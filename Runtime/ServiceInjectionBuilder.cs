using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
			if (fieldsToInject.Count == 0) return;

			var resolutionCts = new CancellationTokenSource();
			ServiceDependencyGraph.RegisterResolving(_targetServiceType, resolutionCts);

			try
			{
				PrepareForInjection(fieldsToInject);
				ThrowIfCircularDependencyDetected();

				CancellationTokenSource timeoutCts = null;
				IDisposable timeoutReg = null;

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
			catch (OperationCanceledException ex)
			{
				// Check if cancellation was due to GameObject destruction or application quit
				if (_cancellationToken.IsCancellationRequested || !Application.isPlaying)
				{
					// The cancellation came from the destroy token, user cancellation, or application quit
					// This is expected behavior - just return silently
					return;
				}
				
				// If we have a timeout and it wasn't from destruction or quit, it's a timeout error
				if (_timeout > 0f)
				{
					var sb = ServiceKitObjectPool.RentStringBuilder();
					try
					{
						sb.Append("Service injection timed out after ");
						sb.Append(_timeout);
						sb.Append(" seconds for target '");
						sb.Append(_target.GetType().Name);
						sb.Append("'.");
						
						var hasMissing = false;
						for (var i = 0; i < fieldsToInject.Count; i++)
						{
							var attr = fieldsToInject[i].GetCustomAttribute<InjectServiceAttribute>();
							if (attr.Required && _serviceKitLocator.GetService(fieldsToInject[i].FieldType) == null)
							{
								if (!hasMissing)
								{
									sb.Append(" Missing required services: ");
									hasMissing = true;
								}
								else
								{
									sb.Append(", ");
								}
								sb.Append(fieldsToInject[i].FieldType.Name);
							}
						}
						if (hasMissing) sb.Append(".");
						
						var circular = ServiceDependencyGraph.DetectCircularDependency(_targetServiceType);
						if (circular != null)
						{
							sb.Append("\n\nCircular dependency detected: ");
							sb.Append(circular.Path);
						}

						throw new TimeoutException(sb.ToString());
					}
					finally
					{
						ServiceKitObjectPool.ReturnStringBuilder(sb);
					}
				}
				
				// Re-throw if it's not a timeout or destruction scenario
				throw;
			}
			finally
			{
				ServiceDependencyGraph.SetResolving(_targetServiceType, false);
				ServiceDependencyGraph.UnregisterResolving(_targetServiceType);
				resolutionCts.Dispose();
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
			var locator = _serviceKitLocator as ServiceKitLocator;
			if (locator == null) return (field, null, serviceAttribute.Required);

			if (locator.IsServiceReady(serviceType))
			{
				var readyService = _serviceKitLocator.GetService(serviceType);
				return (field, readyService, serviceAttribute.Required);
			}

			if (!locator.IsServiceRegistered(serviceType))
			{
				return (field, null, serviceAttribute.Required);
			}

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

		[SerializeField]
		private float _timeout = -1f;

		private readonly IServiceKitLocator _serviceKitLocator;
		private readonly object _target;
		private readonly Type _targetServiceType;
		private CancellationToken _cancellationToken = CancellationToken.None;
		private Action<Exception> _errorHandler;
	}
}
