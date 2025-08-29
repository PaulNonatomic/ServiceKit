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
			var targetType = _target.GetType();
			var fieldsToInject = GetFieldsToInject(targetType);
			if (fieldsToInject.Count == 0)
			{
				return;
			}

			var resolutionCts = new CancellationTokenSource();
			ServiceDependencyGraph.RegisterResolving(_targetServiceType, resolutionCts);

			try
			{
				ServiceDependencyGraph.UpdateForTarget(_targetServiceType, fieldsToInject);
				ServiceDependencyGraph.SetResolving(_targetServiceType, true);

				var circular = ServiceDependencyGraph.DetectCircularDependency(_targetServiceType);
				if (circular != null)
				{
					ServiceDependencyGraph.CancelCircularChain(circular.Path);
					ServiceDependencyGraph.MarkAllInPathAsError(circular.Path);
					throw new ServiceInjectionException($"Circular dependency detected: {circular.Path}");
				}

				CancellationTokenSource timeoutCts = null;
				IDisposable timeoutReg = null;

				if (_timeout > 0f)
				{
					timeoutCts = new CancellationTokenSource();
					timeoutReg = ServiceKitTimeoutManager.Instance.RegisterTimeout(timeoutCts, _timeout);
				}

				using (timeoutReg)
				using (var linked = CancellationTokenSource.CreateLinkedTokenSource(
					_cancellationToken,
					resolutionCts.Token,
					timeoutCts?.Token ?? CancellationToken.None))
				{
					var finalToken = linked.Token;
					var unityContext = SynchronizationContext.Current;

#if SERVICEKIT_UNITASK
					var tasks = fieldsToInject.Select<FieldInfo, UniTask<(FieldInfo field, object service, bool required)>>(async field =>
#else
					var tasks = fieldsToInject.Select<FieldInfo, Task<(FieldInfo field, object service, bool required)>>(async field =>
#endif
					{
						var attr = field.GetCustomAttribute<InjectServiceAttribute>();
						var serviceType = field.FieldType;

						finalToken.ThrowIfCancellationRequested();

						if (!attr.Required)
						{
							var locator = _serviceKitLocator as ServiceKitLocator;
							if (locator == null)
							{
								return (field, null, attr.Required);
							}
							
							// 3-state logic for optional dependencies:
							// 1. Service is ready -> inject immediately
							// 2. Service is registered but not ready -> wait for it (treat as required)
							// 3. Service is not registered -> skip it (inject null)
							
							if (locator.IsServiceReady(serviceType))
							{
								// State 1: Service is ready - inject immediately
								var readyService = _serviceKitLocator.GetService(serviceType);
								return (field, readyService, attr.Required);
							}
							else if (locator.IsServiceRegistered(serviceType))
							{
								// State 2: Service is registered but not ready - wait for it
								try
								{
#if SERVICEKIT_UNITASK
									var pendingService = await _serviceKitLocator.GetServiceAsync(serviceType, finalToken);
#else
									var pendingService = await _serviceKitLocator.GetServiceAsync(serviceType, finalToken);
#endif
									return (field, pendingService, attr.Required);
								}
								catch (OperationCanceledException) when (resolutionCts.IsCancellationRequested)
								{
									ServiceDependencyGraph.AddCircularDependencyError(serviceType);
									ServiceDependencyGraph.AddCircularDependencyError(_targetServiceType);
									throw new ServiceInjectionException($"Injection cancelled due to circular dependency involving {serviceType.Name}");
								}
							}
							else
							{
								// State 3: Service is not registered - skip it
								return (field, null, attr.Required);
							}
						}

						try
						{
#if SERVICEKIT_UNITASK
							var requiredService = await _serviceKitLocator.GetServiceAsync(serviceType, finalToken);
#else
							var requiredService = await _serviceKitLocator.GetServiceAsync(serviceType, finalToken);
#endif
							return (field, requiredService, attr.Required);
						}
						catch (OperationCanceledException) when (resolutionCts.IsCancellationRequested)
						{
							ServiceDependencyGraph.AddCircularDependencyError(serviceType);
							ServiceDependencyGraph.AddCircularDependencyError(_targetServiceType);
							throw new ServiceInjectionException($"Injection cancelled due to circular dependency involving {serviceType.Name}");
						}
					}).ToList();

#if SERVICEKIT_UNITASK
					var results = await UniTask.WhenAll(tasks);
#else
					var results = await Task.WhenAll(tasks);
#endif

					var missing = results
						.Where(r => r.service == null && r.required)
						.Select(r => r.field.FieldType.Name)
						.ToList();

					if (missing.Count > 0)
					{
						throw new ServiceInjectionException($"Required services not available: {string.Join(", ", missing)}");
					}

					await ServiceKitThreading.SwitchToUnityThread(unityContext);

					foreach (var (field, service, _) in results)
					{
						field.SetValue(_target, service);
					}
				}
			}
			catch (OperationCanceledException) when (_timeout > 0f)
			{
				var requiredFields = fieldsToInject.Where(f => f.GetCustomAttribute<InjectServiceAttribute>().Required);
				var missing = requiredFields
					.Where(f => _serviceKitLocator.GetService(f.FieldType) == null)
					.Select(f => f.FieldType.Name)
					.ToList();

				var message = $"Service injection timed out after {_timeout} seconds for target '{_target.GetType().Name}'.";
				if (missing.Count > 0)
				{
					message += $" Missing required services: {string.Join(", ", missing)}.";
				}

				var circular = ServiceDependencyGraph.DetectCircularDependency(_targetServiceType);
				if (circular != null)
				{
					message += $"\n\nCircular dependency detected: {circular.Path}";
				}

				throw new TimeoutException(message);
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

		private void DefaultErrorHandler(Exception exception)
		{
			Debug.LogError($"Failed to inject required services: {exception.Message}");

			var circular = ServiceDependencyGraph.DetectCircularDependency(_targetServiceType);
			if (circular == null)
			{
				return;
			}

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
