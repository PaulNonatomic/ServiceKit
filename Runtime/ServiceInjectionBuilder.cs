using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine;

#if SERVICEKIT_UNITASK
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace Nonatomic.ServiceKit
{
	/// <summary>
	/// Builds and executes service field injection for a target object.
	/// Dependency-graph logic lives in <see cref="ServiceDependencyGraph"/>.
	/// Thread switching lives in <see cref="ServiceKitThreading"/>.
	/// </summary>
	public class ServiceInjectionBuilder : IServiceInjectionBuilder
	{
		public ServiceInjectionBuilder(IServiceKitLocator serviceKitLocator, object target)
		{
			_serviceKitLocator = serviceKitLocator;
			_target = target;
			_targetServiceType = DetermineServiceType(target);

			// The dependency graph is owned by the concrete locator. When the locator is a
			// mock/stub (e.g. in unit tests) there is no graph and circular-dependency
			// bookkeeping is simply skipped.
			_dependencyGraph = (serviceKitLocator as ServiceKitLocator)?.DependencyGraph;
		}

		public IServiceInjectionBuilder WithCancellation(CancellationToken cancellationToken)
		{
			_cancellationToken = cancellationToken;
			return this;
		}

		public IServiceInjectionBuilder WithTimeout()
		{
			_timeout = ServiceKitSettings.Instance.DefaultTimeout;
			return this;
		}

		public IServiceInjectionBuilder WithTimeout(float timeoutSeconds)
		{
			_timeout = timeoutSeconds;
			return this;
		}

		public IServiceInjectionBuilder WithErrorHandling(Action<Exception> errorHandler)
		{
			_errorHandler = errorHandler;
			return this;
		}

		public IServiceInjectionBuilder WithErrorHandling()
		{
			_errorHandler = DefaultErrorHandler;
			return this;
		}

		public async void Execute()
		{
			try
			{
				await ExecuteAsync();
			}
			catch (Exception)
			{
				// The error handler (if any) is already invoked inside ExecuteAsync.
				// Swallow here so the fire-and-forget call doesn't raise an unobserved exception.
			}
		}

#if SERVICEKIT_UNITASK
		public UniTask ExecuteWithCancellationAsync(CancellationToken destroyCancellationToken)
#else
		public Task ExecuteWithCancellationAsync(CancellationToken destroyCancellationToken)
#endif
		{
			return WithCancellation(destroyCancellationToken).ExecuteAsync();
		}


		public async void ExecuteWithCancellation(CancellationToken destroyCancellationToken)
		{
			try
			{
#if SERVICEKIT_UNITASK
				await ExecuteWithCancellationAsync(destroyCancellationToken);
#else
				await ExecuteWithCancellationAsync(destroyCancellationToken);
#endif
			}
			catch (Exception)
			{
				// The error handler (if any) is already invoked inside ExecuteAsync.
				// Swallow here so the fire-and-forget call doesn't raise an unobserved exception.
			}
		}

#if SERVICEKIT_UNITASK
		public async UniTask ExecuteAsync()
#else
		public async Task ExecuteAsync()
#endif
		{
			var fieldsToInject = GetFieldsToInject(_target.GetType());
			if (fieldsToInject.Count == 0) return;

			var resolutionCts = new CancellationTokenSource();
			_dependencyGraph?.RegisterResolving(_targetServiceType, resolutionCts);
			
			CancellationTokenSource timeoutCts = null;
			IDisposable timeoutReg = null;
			SynchronizationContext unityContext = null;

			// Pre-allocate results array so partial results are captured even if WhenAll throws
			var results = new (FieldInfo field, object service, bool required)[fieldsToInject.Count];

			try
			{
				RegisterDependenciesForCircularDetection(fieldsToInject);
				ThrowIfCircularDependencyDetected();

				if (_timeout > 0f)
				{
					timeoutCts = new CancellationTokenSource();
					var timeoutManager = ServiceKitTimeoutManager.Instance;
					if (timeoutManager != null)
					{
						timeoutReg = timeoutManager.RegisterTimeout(timeoutCts, _timeout);
					}
				}

				using (timeoutReg)
				using (var linked = CancellationTokenSource.CreateLinkedTokenSource(
					_cancellationToken,
					resolutionCts.Token,
					timeoutCts?.Token ?? CancellationToken.None))
				{
					var finalToken = linked.Token;
					unityContext = SynchronizationContext.Current;

					await ResolveAllServicesInParallel(fieldsToInject, finalToken, resolutionCts, timeoutCts, results);

					ThrowIfRequiredServicesAreMissing(results);

					await InjectResolvedServices(results, unityContext);
				}
			}
			catch (OperationCanceledException oce)
			{
				var isExplicitTimeout = timeoutCts?.IsCancellationRequested ?? false;

				// Always try to inject any services that were successfully resolved before cancellation
				await TryInjectResolvedServices(results, unityContext);

				if (ShouldIgnoreCancellation(isExplicitTimeout))
				{
					return;
				}

				// Route the failure through the configured handler (if any) before surfacing it.
				var kind = ClassifyCancellation(oce, isExplicitTimeout);
				var injectionException = BuildInjectionException(fieldsToInject, kind);
				_errorHandler?.Invoke(injectionException);
				throw injectionException;
			}
			catch (Exception ex)
			{
				// Covers required-services-missing and circular-dependency failures.
				// The handler is invoked here so WithErrorHandling works on the awaited
				// ExecuteAsync path, not only the fire-and-forget Execute() path.
				_errorHandler?.Invoke(ex);
				throw;
			}
			finally
			{
				_dependencyGraph?.SetResolving(_targetServiceType, false);
				_dependencyGraph?.UnregisterResolving(_targetServiceType);
				resolutionCts.Dispose();
				timeoutCts?.Dispose();
				timeoutReg?.Dispose();
			}
		}

		private static Type DetermineServiceType(object target)
		{
			var targetType = target.GetType();

			// Check for [Service] attribute first
			var serviceAttribute = ServiceKitReflectionCache.GetServiceAttribute(targetType);
			if (serviceAttribute != null && serviceAttribute.ServiceTypes.Length > 0)
			{
				// Return the first registered type as the "primary" type for dependency graph
				return serviceAttribute.ServiceTypes[0];
			}

			// Fallback to concrete type
			return targetType;
		}

		private static List<FieldInfo> GetFieldsToInject(Type targetType)
		{
			// Field discovery is cached per type (see ServiceKitReflectionCache); copy into a fresh
			// list so the caller can treat it as owned.
			return new List<FieldInfo>(ServiceKitReflectionCache.GetInjectableFields(targetType));
		}

		private void RegisterDependenciesForCircularDetection(List<FieldInfo> fieldsToInject)
		{
			_dependencyGraph?.UpdateForTarget(_targetServiceType, fieldsToInject);
			_dependencyGraph?.SetResolving(_targetServiceType, true);
		}

#if SERVICEKIT_UNITASK
		private async UniTask<(FieldInfo field, object service, bool required)[]> ResolveAllServicesInParallel(
			List<FieldInfo> fieldsToInject,
			CancellationToken cancellationToken,
			CancellationTokenSource resolutionCts,
			CancellationTokenSource timeoutCts,
			(FieldInfo field, object service, bool required)[] sharedResults)
		{
			var taskCount = fieldsToInject.Count;
			var tasks = new UniTask[taskCount];

			for (int i = 0; i < taskCount; i++)
			{
				var index = i;
				tasks[i] = ResolveAndStoreResult(fieldsToInject[i], cancellationToken, resolutionCts, sharedResults, index);
			}

			await UniTask.WhenAll(tasks);
			return sharedResults;
		}
#else
		private async Task<(FieldInfo field, object service, bool required)[]> ResolveAllServicesInParallel(
			List<FieldInfo> fieldsToInject,
			CancellationToken cancellationToken,
			CancellationTokenSource resolutionCts,
			CancellationTokenSource timeoutCts,
			(FieldInfo field, object service, bool required)[] sharedResults)
		{
			var taskCount = fieldsToInject.Count;
			var tasks = new Task[taskCount];

			for (int i = 0; i < taskCount; i++)
			{
				var index = i;
				tasks[i] = ResolveAndStoreResult(fieldsToInject[i], cancellationToken, resolutionCts, sharedResults, index);
			}

			await Task.WhenAll(tasks);
			return sharedResults;
		}
#endif

#if SERVICEKIT_UNITASK
		private async UniTask ResolveAndStoreResult(
			FieldInfo field,
			CancellationToken cancellationToken,
			CancellationTokenSource resolutionCts,
			(FieldInfo field, object service, bool required)[] results,
			int index)
#else
		private async Task ResolveAndStoreResult(
			FieldInfo field,
			CancellationToken cancellationToken,
			CancellationTokenSource resolutionCts,
			(FieldInfo field, object service, bool required)[] results,
			int index)
#endif
		{
			var attr = ServiceKitReflectionCache.GetInjectAttribute(field);
			try
			{
				var result = await ResolveServiceForField(field, cancellationToken, resolutionCts);
				results[index] = result;
			}
			catch (OperationCanceledException)
			{
				// Store null result before re-throwing - this captures partial results
				results[index] = (field, null, attr?.Required ?? true);
				throw;
			}
			catch (ServiceInjectionException)
			{
				// Store null result for circular dependency errors
				results[index] = (field, null, attr?.Required ?? true);
				throw;
			}
		}

		private void ThrowIfRequiredServicesAreMissing((FieldInfo field, object service, bool required)[] results)
		{
			var missingRequiredServices = results
				.Where(r => r.field != null && r.service == null && r.required)
				.Select(r => r.field.FieldType.Name)
				.ToArray();

			if (missingRequiredServices.Length == 0)
				return;

			var sb = ServiceKitObjectPool.RentStringBuilder();
			try
			{
				sb.Append("Required services not available: ");
				sb.Append(string.Join(", ", missingRequiredServices));
				throw new ServiceInjectionException(sb.ToString());
			}
			finally
			{
				ServiceKitObjectPool.ReturnStringBuilder(sb);
			}
		}

		private void ThrowIfCircularDependencyDetected()
		{
			var circularDependency = _dependencyGraph?.DetectCircularDependency(_targetServiceType);
			if (circularDependency == null) return;

			_dependencyGraph?.CancelCircularChain(circularDependency.TypesInPath);
			_dependencyGraph?.MarkAllInPathAsError(circularDependency.TypesInPath);
			throw new ServiceInjectionException($"Circular dependency detected: {circularDependency.Path}");
		}

#if SERVICEKIT_UNITASK
		private async UniTask<(FieldInfo field, object service, bool required)> ResolveServiceForField(FieldInfo field, CancellationToken cancellationToken, CancellationTokenSource resolutionCts)
#else
		private async Task<(FieldInfo field, object service, bool required)> ResolveServiceForField(FieldInfo field, CancellationToken cancellationToken, CancellationTokenSource resolutionCts)
#endif
		{
			var serviceAttribute = ServiceKitReflectionCache.GetInjectAttribute(field);
			var serviceType = field.FieldType;

			cancellationToken.ThrowIfCancellationRequested();

			if (!serviceAttribute.Required)
			{
				return await ResolveOptionalService(field, serviceType, serviceAttribute, cancellationToken, resolutionCts);
			}

			return await ResolveRequiredService(field, serviceType, serviceAttribute, cancellationToken, resolutionCts);
		}

#if SERVICEKIT_UNITASK
		private async UniTask<(FieldInfo field, object service, bool required)> ResolveOptionalService(FieldInfo field, Type serviceType, InjectServiceAttribute serviceAttribute, CancellationToken cancellationToken, CancellationTokenSource resolutionCts)
#else
		private async Task<(FieldInfo field, object service, bool required)> ResolveOptionalService(FieldInfo field, Type serviceType, InjectServiceAttribute serviceAttribute, CancellationToken cancellationToken, CancellationTokenSource resolutionCts)
#endif
		{
			var locator = _serviceKitLocator as ServiceKitLocator;
			if (locator == null) return (field, null, serviceAttribute.Required);

			// Atomic check: determines if the service is ready, registered-but-not-ready, or absent
			var status = locator.TryResolveService(serviceType, out var readyService);

			if (status == ServiceResolutionStatus.Ready)
			{
				return (field, readyService, serviceAttribute.Required);
			}

			if (status == ServiceResolutionStatus.NotRegistered)
			{
				// Give one frame for late-registering services (e.g., other Awake calls)
				await WaitForAwakePhaseCompletion();

				// Re-check atomically after the yield
				status = locator.TryResolveService(serviceType, out readyService);

				if (status == ServiceResolutionStatus.Ready)
				{
					return (field, readyService, serviceAttribute.Required);
				}

				if (status == ServiceResolutionStatus.NotRegistered)
				{
					return (field, null, serviceAttribute.Required);
				}
			}

			// Service is registered but not yet ready — wait for it
			try
			{
				var pendingService = await _serviceKitLocator.GetServiceAsync(serviceType, cancellationToken);
				return (field, pendingService, serviceAttribute.Required);
			}
			catch (OperationCanceledException) when (resolutionCts.IsCancellationRequested)
			{
				HandleCircularDependencyError(serviceType);
				throw new ServiceInjectionException($"Injection cancelled due to circular dependency involving {serviceType.Name}");
			}
			catch (OperationCanceledException)
			{
				throw;
			}
		}

#if SERVICEKIT_UNITASK
		private async UniTask<(FieldInfo field, object service, bool required)> ResolveRequiredService(FieldInfo field, Type serviceType, InjectServiceAttribute serviceAttribute, CancellationToken cancellationToken, CancellationTokenSource resolutionCts)
#else
		private async Task<(FieldInfo field, object service, bool required)> ResolveRequiredService(FieldInfo field, Type serviceType, InjectServiceAttribute serviceAttribute, CancellationToken cancellationToken, CancellationTokenSource resolutionCts)
#endif
		{
			try
			{
				var requiredService = await _serviceKitLocator.GetServiceAsync(serviceType, cancellationToken);
				return (field, requiredService, serviceAttribute.Required);
			}
			catch (OperationCanceledException) when (resolutionCts.IsCancellationRequested)
			{
				HandleCircularDependencyError(serviceType);
				throw new ServiceInjectionException($"Injection cancelled due to circular dependency involving {serviceType.Name}");
			}
		}

		private void HandleCircularDependencyError(Type serviceType)
		{
			_dependencyGraph?.AddCircularDependencyError(serviceType);
			_dependencyGraph?.AddCircularDependencyError(_targetServiceType);
		}

#if SERVICEKIT_UNITASK
		private async UniTask WaitForAwakePhaseCompletion()
		{
			// Yield to allow other Awake calls to complete registration
			await UniTask.NextFrame();
		}
#else
		private async Task WaitForAwakePhaseCompletion()
		{
			// Yield to allow other Awake calls to complete registration.
			// Task.Yield doesn't properly defer in Unity Edit Mode, so we use Task.Delay
			// as a fallback. This is imprecise but sufficient since we re-check atomically after.
			await Task.Delay(1);
		}
#endif

		private void DefaultErrorHandler(Exception exception)
		{
			ServiceInjectionLog.Report(exception, _target as UnityEngine.Object);

			var circular = _dependencyGraph?.DetectCircularDependency(_targetServiceType);
			if (circular == null) return;

			Debug.LogError($"Circular dependency detected: {circular.Path}", _target as UnityEngine.Object);
			_dependencyGraph?.AddCircularDependencyError(_targetServiceType);

			if (circular.ToType != null)
			{
				_dependencyGraph?.AddCircularDependencyError(circular.ToType);
			}
		}

		public Type TargetServiceType => _targetServiceType;

		private bool ShouldIgnoreCancellation(bool isExplicitTimeout)
		{
			// Ignore cancellation if it's from application quit and not an explicit timeout or user cancellation
			return !isExplicitTimeout && !_cancellationToken.IsCancellationRequested && !Application.isPlaying;
		}

		private ServiceInjectionFailureKind ClassifyCancellation(Exception exception, bool isExplicitTimeout)
		{
			// A destroyed target takes precedence: the injection is moot regardless of why it ended.
			// Unity's overloaded null check is the only reliable "target destroyed" signal.
			var targetIsUnityObject = _target is UnityEngine.Object;
			if (targetIsUnityObject && (UnityEngine.Object)_target == null)
			{
				return ServiceInjectionFailureKind.TargetDestroyed;
			}

			// Positively signalled by the locator when an awaited service is unregistered
			// (directly, via scene unload, or on ClearServices) - no inference required.
			if (exception is ServiceUnregisteredException)
			{
				return ServiceInjectionFailureKind.ServiceUnregistered;
			}

			// Positively signalled: only the timeout manager cancels timeoutCts.
			if (isExplicitTimeout)
			{
				return ServiceInjectionFailureKind.Timeout;
			}

			if (_cancellationToken.IsCancellationRequested)
			{
				// For a Unity target this is its destroyCancellationToken firing (scene change /
				// teardown); for a plain C# object it is a deliberate caller cancellation.
				return targetIsUnityObject
					? ServiceInjectionFailureKind.TargetDestroyed
					: ServiceInjectionFailureKind.CallerCanceled;
			}

			// Residual: a bare cancellation with no token and no typed cause. Nothing positively
			// indicates a timeout, so treat it as a vanished service (warning) rather than an error.
			return ServiceInjectionFailureKind.ServiceUnregistered;
		}

		private ServiceInjectionTimeoutException BuildInjectionException(List<FieldInfo> fieldsToInject, ServiceInjectionFailureKind kind)
		{
			var sb = ServiceKitObjectPool.RentStringBuilder();
			try
			{
				AppendFailureMessage(sb, kind);
				AppendWaitingServices(sb, fieldsToInject);
				AppendCircularDependencyInfo(sb);

				return new ServiceInjectionTimeoutException(sb.ToString(), kind);
			}
			finally
			{
				ServiceKitObjectPool.ReturnStringBuilder(sb);
			}
		}

		private void AppendFailureMessage(StringBuilder sb, ServiceInjectionFailureKind kind)
		{
			switch (kind)
			{
				case ServiceInjectionFailureKind.Timeout:
					sb.Append("Service injection timed out");
					if (_timeout > 0f)
					{
						sb.Append(" after ");
						sb.Append(_timeout);
						sb.Append(" seconds");
					}
					sb.Append(" for ");
					sb.Append(DescribeTarget());
					sb.Append('.');
					break;

				case ServiceInjectionFailureKind.ServiceUnregistered:
					sb.Append("Service injection for ");
					sb.Append(DescribeTarget());
					sb.Append(" was interrupted because a required service was unregistered before it became available (e.g. a scene change).");
					break;

				case ServiceInjectionFailureKind.TargetDestroyed:
					sb.Append("Service injection for ");
					sb.Append(DescribeTarget());
					sb.Append(" was canceled because the target was destroyed (e.g. a scene change).");
					break;

				default: // CallerCanceled
					sb.Append("Service injection for ");
					sb.Append(DescribeTarget());
					sb.Append(" was canceled via its cancellation token.");
					break;
			}
		}

		/// <summary>
		/// Describes the injection target for error messages. For a live Component this includes
		/// the GameObject's hierarchy path and scene, so the offending object is easy to locate.
		/// Guarded against Unity fake-null in case the target is mid-destruction.
		/// </summary>
		private string DescribeTarget()
		{
			var typeName = _target.GetType().Name;

			if (_target is Component component && component != null)
			{
				var path = GetHierarchyPath(component.transform);
				var scene = component.gameObject.scene;
				return scene.IsValid()
					? $"'{typeName}' on '{path}' (scene '{scene.name}')"
					: $"'{typeName}' on '{path}'";
			}

			return $"'{typeName}'";
		}

		private static string GetHierarchyPath(Transform transform)
		{
			// Error path — a plain StringBuilder is fine and avoids nesting the shared pool.
			var sb = new StringBuilder(transform.name);
			for (var parent = transform.parent; parent != null; parent = parent.parent)
			{
				sb.Insert(0, '/');
				sb.Insert(0, parent.name);
			}
			return sb.ToString();
		}

		private void AppendWaitingServices(StringBuilder sb, List<FieldInfo> fieldsToInject)
		{
			var hasMissing = false;
			var locator = _serviceKitLocator as ServiceKitLocator;
			
			for (var i = 0; i < fieldsToInject.Count; i++)
			{
				var field = fieldsToInject[i];
				var attr = ServiceKitReflectionCache.GetInjectAttribute(field);
				var serviceType = field.FieldType;
				
				if (IsServiceMissing(serviceType, attr.Required, locator))
				{
					if (!hasMissing)
					{
						sb.Append(" Waiting for services: ");
						hasMissing = true;
					}
					else
					{
						sb.Append(", ");
					}
					
					sb.Append(serviceType.Name);
					
					if (!attr.Required && locator?.IsServiceRegistered(serviceType) == true)
					{
						sb.Append(" (optional but registered)");
					}
				}
			}
			
			if (hasMissing) 
				sb.Append(".");
		}

		private bool IsServiceMissing(Type serviceType, bool isRequired, ServiceKitLocator locator)
		{
			var service = _serviceKitLocator.GetService(serviceType);
			var isRegistered = locator?.IsServiceRegistered(serviceType) ?? false;
			
			// Service is missing if it's null and either required or registered (but not ready)
			return service == null && (isRequired || isRegistered);
		}

		private void AppendCircularDependencyInfo(StringBuilder sb)
		{
			var circular = _dependencyGraph?.DetectCircularDependency(_targetServiceType);
			if (circular != null)
			{
				sb.Append("\n\nCircular dependency detected: ");
				sb.Append(circular.Path);
			}
		}

#if SERVICEKIT_UNITASK
		private async UniTask InjectResolvedServices((FieldInfo field, object service, bool required)[] results, SynchronizationContext unityContext)
#else
		private async Task InjectResolvedServices((FieldInfo field, object service, bool required)[] results, SynchronizationContext unityContext)
#endif
		{
			if (results == null || results.Length == 0)
				return;

			await ServiceKitThreading.SwitchToUnityThread(unityContext);

			foreach (var (field, service, _) in results)
			{
				// Skip entries that weren't populated (can happen with pre-allocated array)
				if (field == null)
					continue;

				field.SetValue(_target, service);
			}
		}

#if SERVICEKIT_UNITASK
		private async UniTask TryInjectResolvedServices((FieldInfo field, object service, bool required)[] results, SynchronizationContext unityContext)
#else
		private async Task TryInjectResolvedServices((FieldInfo field, object service, bool required)[] results, SynchronizationContext unityContext)
#endif
		{
			if (results == null || results.Length == 0)
				return;

			try
			{
				await InjectResolvedServices(results, unityContext);
			}
			catch (Exception ex)
			{
				Debug.LogError($"Failed to inject resolved services during cancellation: {ex.Message}");
			}
		}

		[SerializeField]
		private float _timeout = -1f;

		private readonly IServiceKitLocator _serviceKitLocator;
		private readonly ServiceDependencyGraph _dependencyGraph;
		private readonly object _target;
		private readonly Type _targetServiceType;
		private CancellationToken _cancellationToken = CancellationToken.None;
		private Action<Exception> _errorHandler;
	}
}
