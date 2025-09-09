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
		public static bool IsExemptFromCircularDependencyCheck(Type serviceType)
			=> ServiceDependencyGraph.IsCircularDependencyExempt(serviceType);

		public static bool HasCircularDependencyError(Type serviceType)
			=> ServiceDependencyGraph.HasCircularDependencyError(serviceType);
		
		public ServiceInjectionBuilder(IServiceKitLocator serviceKitLocator, object target)
		{
			_serviceKitLocator = serviceKitLocator;
			_target = target;
			_targetServiceType = DetermineServiceType(target);
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
			catch (Exception ex)
			{
				_errorHandler?.Invoke(ex);
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
			catch (Exception ex)
			{
				_errorHandler?.Invoke(ex);
			}
		}

#if SERVICEKIT_UNITASK
		public async UniTask ExecuteAsync()
#else
		public async Task ExecuteAsync()
#endif
		{
			var fieldsToInject = GetFieldsToInject(_target.GetType());
#if UNITY_EDITOR
			if (ServiceKitSettings.Instance.DebugLogging)
			{
				Debug.Log($"[ServiceKit] Found {fieldsToInject.Count} fields to inject for {_target.GetType().Name}");
				foreach (var field in fieldsToInject)
				{
					var attr = field.GetCustomAttribute<InjectServiceAttribute>();
					Debug.Log($"[ServiceKit]   - Field: {field.Name}, Type: {field.FieldType.Name}, Required: {attr.Required}");
				}
			}
#endif
			if (fieldsToInject.Count == 0) return;

			var resolutionCts = new CancellationTokenSource();
			ServiceDependencyGraph.RegisterResolving(_targetServiceType, resolutionCts);
			
			CancellationTokenSource timeoutCts = null;
			IDisposable timeoutReg = null;

			try
			{
				PrepareForInjection(fieldsToInject);
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
					var unityContext = SynchronizationContext.Current;

					var taskCount = fieldsToInject.Count;
#if SERVICEKIT_UNITASK
					var tasks = new UniTask<(FieldInfo field, object service, bool required)>[taskCount];
#else
					var tasks = new Task<(FieldInfo field, object service, bool required)>[taskCount];
#endif
					for (int i = 0; i < taskCount; i++)
					{
						tasks[i] = ResolveServiceForField(fieldsToInject[i], finalToken, resolutionCts);
					}

#if SERVICEKIT_UNITASK
					var results = await UniTask.WhenAll(tasks);
#else
					var results = await Task.WhenAll(tasks);
#endif

					var missingCount = 0;
					for (var i = 0; i < results.Length; i++)
					{
						if (results[i].service == null && results[i].required)
						{
							missingCount++;
						}
					}

					if (missingCount > 0)
					{
						var sb = ServiceKitObjectPool.RentStringBuilder();
						try
						{
							sb.Append("Required services not available: ");
							var first = true;
							for (var i = 0; i < results.Length; i++)
							{
								if (results[i].service == null && results[i].required)
								{
									if (!first) sb.Append(", ");
									sb.Append(results[i].field.FieldType.Name);
									first = false;
								}
							}
							throw new ServiceInjectionException(sb.ToString());
						}
						finally
						{
							ServiceKitObjectPool.ReturnStringBuilder(sb);
						}
					}

					await ServiceKitThreading.SwitchToUnityThread(unityContext);

					foreach (var (field, service, _) in results)
					{
						field.SetValue(_target, service);
					}
				}
			}
			catch (OperationCanceledException)
			{
				bool isExplicitTimeout = timeoutCts?.IsCancellationRequested ?? false;
				
				if (ShouldIgnoreCancellation(isExplicitTimeout))
					return;
				
				
				throw BuildTimeoutException(fieldsToInject, isExplicitTimeout);
			}
			finally
			{
				ServiceDependencyGraph.SetResolving(_targetServiceType, false);
				ServiceDependencyGraph.UnregisterResolving(_targetServiceType);
				resolutionCts.Dispose();
				timeoutCts?.Dispose();
				timeoutReg?.Dispose();
			}
		}

		private static Type DetermineServiceType(object target)
		{
			var targetType = target.GetType();
			var baseType = targetType.BaseType;

			while (baseType != null)
			{
				if (baseType.IsGenericType &&
					baseType.GetGenericTypeDefinition().Name.StartsWith("ServiceKitBehaviour", StringComparison.Ordinal))
				{
					return baseType.GetGenericArguments()[0];
				}
				baseType = baseType.BaseType;
			}

			return targetType;
		}

		private static List<FieldInfo> GetFieldsToInject(Type targetType)
		{
			var fields = new List<FieldInfo>();
			var current = targetType;

			while (current != null && current != typeof(object))
			{
				var typeFields = current
					.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)
					.Where(f => f.GetCustomAttribute<InjectServiceAttribute>() != null);

				fields.AddRange(typeFields);
				current = current.BaseType;
			}

			return fields;
		}

		private void PrepareForInjection(List<FieldInfo> fieldsToInject)
		{
			ServiceDependencyGraph.UpdateForTarget(_targetServiceType, fieldsToInject);
			ServiceDependencyGraph.SetResolving(_targetServiceType, true);
		}

		private void ThrowIfCircularDependencyDetected()
		{
			var circularDependency = ServiceDependencyGraph.DetectCircularDependency(_targetServiceType);
			if (circularDependency == null) return;

			ServiceDependencyGraph.CancelCircularChain(circularDependency.Path);
			ServiceDependencyGraph.MarkAllInPathAsError(circularDependency.Path);
			throw new ServiceInjectionException($"Circular dependency detected: {circularDependency.Path}");
		}

#if SERVICEKIT_UNITASK
		private async UniTask<(FieldInfo field, object service, bool required)> ResolveServiceForField(FieldInfo field, CancellationToken cancellationToken, CancellationTokenSource resolutionCts)
#else
		private async Task<(FieldInfo field, object service, bool required)> ResolveServiceForField(FieldInfo field, CancellationToken cancellationToken, CancellationTokenSource resolutionCts)
#endif
		{
			var serviceAttribute = field.GetCustomAttribute<InjectServiceAttribute>();
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
			// Optional dependencies follow this behavior:
			// - If service is ready: inject immediately
			// - If service is registered but not ready: wait for it (treat as temporarily required)
			// - If service is not registered: return null immediately (truly optional)
			
			var locator = _serviceKitLocator as ServiceKitLocator;
			if (locator == null) return (field, null, serviceAttribute.Required);

#if UNITY_EDITOR
			if (ServiceKitSettings.Instance.DebugLogging)
			{
				Debug.Log($"[ServiceKit] Resolving optional service {serviceType.Name}");
			}
#endif

			if (locator.IsServiceReady(serviceType))
			{
				var readyService = _serviceKitLocator.GetService(serviceType);
#if UNITY_EDITOR
				if (ServiceKitSettings.Instance.DebugLogging)
				{
					Debug.Log($"[ServiceKit] Optional service {serviceType.Name} is ready, injecting immediately");
				}
#endif
				return (field, readyService, serviceAttribute.Required);
			}

			if (!locator.IsServiceRegistered(serviceType))
			{
#if UNITY_EDITOR
				if (ServiceKitSettings.Instance.DebugLogging)
				{
					Debug.Log($"[ServiceKit] Optional service {serviceType.Name} is not registered, skipping");
				}
#endif
				return (field, null, serviceAttribute.Required);
			}

#if UNITY_EDITOR
			if (ServiceKitSettings.Instance.DebugLogging)
			{
				Debug.Log($"[ServiceKit] Optional service {serviceType.Name} is registered but not ready, waiting...");
			}
#endif

			try
			{
				var pendingService = await _serviceKitLocator.GetServiceAsync(serviceType, cancellationToken);
#if UNITY_EDITOR
				if (ServiceKitSettings.Instance.DebugLogging)
				{
					Debug.Log($"[ServiceKit] Optional service {serviceType.Name} became ready and was injected");
				}
#endif
				return (field, pendingService, serviceAttribute.Required);
			}
			catch (OperationCanceledException) when (resolutionCts.IsCancellationRequested)
			{
				HandleCircularDependencyError(serviceType);
				throw new ServiceInjectionException($"Injection cancelled due to circular dependency involving {serviceType.Name}");
			}
			catch (OperationCanceledException)
			{
				// For optional services that are registered but not ready, we should propagate the cancellation
				// This ensures that the injection waits for registered services even if they're optional
#if UNITY_EDITOR
				if (ServiceKitSettings.Instance.DebugLogging)
				{
					Debug.Log($"[ServiceKit] Optional service {serviceType.Name} timed out or was cancelled");
				}
#endif
				// Re-throw to propagate the timeout/cancellation
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
			ServiceDependencyGraph.AddCircularDependencyError(serviceType);
			ServiceDependencyGraph.AddCircularDependencyError(_targetServiceType);
		}

		private void DefaultErrorHandler(Exception exception)
		{
			Debug.LogError($"Failed to inject required services: {exception.Message}");

			var circular = ServiceDependencyGraph.DetectCircularDependency(_targetServiceType);
			if (circular == null) return;

			Debug.LogError($"Circular dependency detected: {circular.Path}");
			ServiceDependencyGraph.AddCircularDependencyError(_targetServiceType);

			if (circular.ToType != null)
			{
				ServiceDependencyGraph.AddCircularDependencyError(circular.ToType);
			}
		}

		public Type TargetServiceType => _targetServiceType;

		private bool ShouldIgnoreCancellation(bool isExplicitTimeout)
		{
			// Ignore cancellation if it's from application quit and not an explicit timeout or user cancellation
			return !isExplicitTimeout && !_cancellationToken.IsCancellationRequested && !Application.isPlaying;
		}

		private TimeoutException BuildTimeoutException(List<FieldInfo> fieldsToInject, bool isExplicitTimeout)
		{
			var sb = ServiceKitObjectPool.RentStringBuilder();
			try
			{
				AppendTimeoutMessage(sb, isExplicitTimeout);
				AppendWaitingServices(sb, fieldsToInject);
				AppendCircularDependencyInfo(sb);
				
				return new TimeoutException(sb.ToString());
			}
			finally
			{
				ServiceKitObjectPool.ReturnStringBuilder(sb);
			}
		}

		private void AppendTimeoutMessage(StringBuilder sb, bool isExplicitTimeout)
		{
			sb.Append("Service injection timed out");
			
			if (isExplicitTimeout && _timeout > 0f)
			{
				sb.Append(" after ");
				sb.Append(_timeout);
				sb.Append(" seconds");
			}
			else if (_cancellationToken.IsCancellationRequested)
			{
				sb.Append(" (via cancellation token)");
			}
			
			sb.Append(" for target '");
			sb.Append(_target.GetType().Name);
			sb.Append("'.");
		}

		private void AppendWaitingServices(StringBuilder sb, List<FieldInfo> fieldsToInject)
		{
			var hasMissing = false;
			var locator = _serviceKitLocator as ServiceKitLocator;
			
			for (var i = 0; i < fieldsToInject.Count; i++)
			{
				var field = fieldsToInject[i];
				var attr = field.GetCustomAttribute<InjectServiceAttribute>();
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
			var circular = ServiceDependencyGraph.DetectCircularDependency(_targetServiceType);
			if (circular != null)
			{
				sb.Append("\n\nCircular dependency detected: ");
				sb.Append(circular.Path);
			}
		}

		[SerializeField]
		private float _timeout = -1f;

		private readonly IServiceKitLocator _serviceKitLocator;
		private readonly object _target;
		private readonly Type _targetServiceType;
		private CancellationToken _cancellationToken = CancellationToken.None;
		private Action<Exception> _errorHandler;
	}
}
