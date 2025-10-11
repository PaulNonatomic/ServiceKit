using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

#if SERVICEKIT_UNITASK
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace Nonatomic.ServiceKit
{
	[CreateAssetMenu(fileName = "ServiceKit", menuName = "ServiceKit/ServiceKitLocator")]
	public class ServiceKitLocator : ScriptableObject, IServiceKitLocator
	{
		private readonly Dictionary<Type, ServiceInfo> _readyServices = new Dictionary<Type, ServiceInfo>();
		private readonly Dictionary<Type, RegisteredServiceInfo> _registeredServices = new Dictionary<Type, RegisteredServiceInfo>();

#if SERVICEKIT_UNITASK
		private readonly Dictionary<Type, UniTaskCompletionSource<object>> _serviceAwaiters = new Dictionary<Type, UniTaskCompletionSource<object>>();
#else
		private readonly Dictionary<Type, TaskCompletionSource<object>> _serviceAwaiters = new Dictionary<Type, TaskCompletionSource<object>>();
#endif

		private readonly object _lock = new object();
		private readonly HashSet<Scene> _trackedScenes = new HashSet<Scene>();

		private class RegisteredServiceInfo
		{
			public ServiceInfo ServiceInfo { get; set; }
#if UNITY_EDITOR
			public DateTime RegisteredAt { get; set; }
			public List<Type> WaitingForDependencies { get; set; } = new List<Type>();
#endif
		}

		public void RegisterService<T>(T service, [CallerMemberName] string registeredBy = null) where T : class
		{
			RegisterService(service, false, null, registeredBy);
		}

		public void RegisterService<T>(T service, ServiceTag[] tags, [CallerMemberName] string registeredBy = null) where T : class
		{
			RegisterService(service, false, tags, registeredBy);
		}

		public void RegisterServiceWithCircularExemption<T>(T service, [CallerMemberName] string registeredBy = null) where T : class
		{
			RegisterService(service, true, null, registeredBy);
		}

		public void RegisterServiceWithCircularExemption<T>(T service, ServiceTag[] tags, [CallerMemberName] string registeredBy = null) where T : class
		{
			RegisterService(service, true, tags, registeredBy);
		}

		private void RegisterService<T>(T service, bool exemptFromCircularDependencyCheck, ServiceTag[] tags, [CallerMemberName] string registeredBy = null) where T : class
		{
			ValidateServiceNotNull<T>(service, registeredBy);

			lock (_lock)
			{
				var serviceType = typeof(T);
				var serviceInfo = CreateServiceInfo(service, serviceType, registeredBy, tags);

				if (_readyServices.ContainsKey(serviceType))
				{
#if UNITY_EDITOR
					if (ServiceKitSettings.Instance.DebugLogging)
					{
						Debug.LogWarning($"[ServiceKit] Service {serviceType.Name} is already ready. Use UnregisterService first.");
					}
#endif
					return;
				}

				if (_registeredServices.ContainsKey(serviceType))
				{
#if UNITY_EDITOR
					if (ServiceKitSettings.Instance.DebugLogging)
					{
						Debug.LogWarning($"[ServiceKit] Service {serviceType.Name} is already registered. Updating registration.");
					}
#endif
				}

				// Track circular dependency exemption
				if (exemptFromCircularDependencyCheck)
				{
					ServiceDependencyGraph.AddCircularDependencyExemption(serviceType);
#if UNITY_EDITOR
					if (ServiceKitSettings.Instance.DebugLogging)
					{
						Debug.Log($"[ServiceKit] Service {serviceType.Name} is exempt from circular dependency checks");
					}
#endif
				}

				// Analyze dependencies and check for circular dependencies at registration time
				if (!exemptFromCircularDependencyCheck)
				{
					var fieldsToInject = GetInjectableFields(service.GetType());
					ServiceDependencyGraph.UpdateForRegistration(serviceType, fieldsToInject);

					var circularDependency = ServiceDependencyGraph.DetectCircularDependencyAtRegistration(serviceType);
					if (circularDependency != null)
					{
						ServiceDependencyGraph.AddCircularDependencyError(serviceType);

						MarkCircularDependencyChainAsError(circularDependency);

#if UNITY_EDITOR
						if (ServiceKitSettings.Instance.DebugLogging)
						{
							Debug.LogError($"[ServiceKit] Circular dependency detected during registration: {circularDependency}");
						}
#endif
					}
				}

				if (service is MonoBehaviour monoBehaviour && monoBehaviour != null)
				{
					TrackServiceScene(monoBehaviour);
				}

				var registeredInfo = new RegisteredServiceInfo
				{
					ServiceInfo = serviceInfo
				};
#if UNITY_EDITOR
				registeredInfo.RegisteredAt = DateTime.Now;
#endif
				_registeredServices[serviceType] = registeredInfo;

#if UNITY_EDITOR
				if (ServiceKitSettings.Instance.DebugLogging)
				{
					Debug.Log($"[ServiceKit] Registered {serviceType.Name} (not ready yet) from scene '{serviceInfo.DebugData.SceneName}' by {registeredBy}");
				}
#endif
			}
		}

		public void ReadyService<T>() where T : class
		{
			ReadyService(typeof(T));
		}

		public void ReadyService(Type serviceType)
		{
			RegisteredServiceInfo registeredInfo;

			lock (_lock)
			{
				if (!_registeredServices.TryGetValue(serviceType, out registeredInfo))
				{
					throw new InvalidOperationException($"Cannot ready service {serviceType.Name} - it must be registered first");
				}

				if (_readyServices.ContainsKey(serviceType))
				{
#if UNITY_EDITOR
					if (ServiceKitSettings.Instance.DebugLogging)
					{
						Debug.LogWarning($"Service {serviceType.Name} is already ready");
					}
#endif
					return;
				}

				_registeredServices.Remove(serviceType);
				_readyServices[serviceType] = registeredInfo.ServiceInfo;

#if UNITY_EDITOR
				if (ServiceKitSettings.Instance.DebugLogging)
				{
					var initTime = (DateTime.Now - registeredInfo.RegisteredAt).TotalMilliseconds;
					Debug.Log($"[ServiceKit] Service {serviceType.Name} is now READY (init took {initTime:F0}ms)");
				}
#endif
			}

			CompleteAwaiters(serviceType, registeredInfo.ServiceInfo.Service);
			CheckWaitingServices(serviceType);
		}

		public void RegisterAndReadyService<T>(T service, [CallerMemberName] string registeredBy = null) where T : class
		{
			RegisterService(service, false, null, registeredBy);
			ReadyService<T>();
		}

		public void RegisterAndReadyService<T>(T service, ServiceTag[] tags, [CallerMemberName] string registeredBy = null) where T : class
		{
			RegisterService(service, false, tags, registeredBy);
			ReadyService<T>();
		}

		public void RegisterAndReadyServiceWithCircularExemption<T>(T service, [CallerMemberName] string registeredBy = null) where T : class
		{
			RegisterService(service, true, null, registeredBy);
			ReadyService<T>();
		}

		public void RegisterAndReadyServiceWithCircularExemption<T>(T service, ServiceTag[] tags, [CallerMemberName] string registeredBy = null) where T : class
		{
			RegisterService(service, true, tags, registeredBy);
			ReadyService<T>();
		}

		public T GetService<T>() where T : class
		{
			return GetService(typeof(T)) as T;
		}

		public object GetService(Type serviceType)
		{
			lock (_lock)
			{
				return _readyServices.TryGetValue(serviceType, out var serviceInfo)
					? serviceInfo.Service
					: null;
			}
		}

		public bool IsServiceRegistered<T>() where T : class
		{
			return IsServiceRegistered(typeof(T));
		}

		public bool IsServiceRegistered(Type serviceType)
		{
			lock (_lock)
			{
				return _registeredServices.ContainsKey(serviceType) || _readyServices.ContainsKey(serviceType);
			}
		}

		public bool IsServiceReady<T>() where T : class
		{
			return IsServiceReady(typeof(T));
		}

		public bool IsServiceReady(Type serviceType)
		{
			lock (_lock)
			{
				return _readyServices.ContainsKey(serviceType);
			}
		}

		public bool IsServiceCircularDependencyExempt<T>() where T : class
		{
			return IsServiceCircularDependencyExempt(typeof(T));
		}

		public bool IsServiceCircularDependencyExempt(Type serviceType)
		{
			return ServiceDependencyGraph.IsCircularDependencyExempt(serviceType);
		}

		public bool TryGetService<T>(out T service) where T : class
		{
			if (TryGetService(typeof(T), out var serviceObj))
			{
				service = serviceObj as T;
				return service != null;
			}

			service = null;
			return false;
		}

		public bool TryGetService(Type serviceType, out object service)
		{
			lock (_lock)
			{
				if (_readyServices.TryGetValue(serviceType, out var serviceInfo))
				{
					service = serviceInfo.Service;
					return service != null;
				}

				service = null;
				return false;
			}
		}

#if SERVICEKIT_UNITASK
		private async UniTaskVoid ForwardTaskResult(UniTaskCompletionSource<object> source, UniTaskCompletionSource<object> target)
		{
			try
			{
				var result = await source.Task;
				target.TrySetResult(result);
			}
			catch (Exception ex)
			{
				target.TrySetException(ex);
			}
		}
		
		public async UniTask<T> GetServiceAsync<T>(CancellationToken cancellationToken = default) where T : class
		{
			var service = await GetServiceAsync(typeof(T), cancellationToken);
			return service as T;
		}

		public async UniTask<object> GetServiceAsync(Type serviceType, CancellationToken cancellationToken = default)
		{
			UniTaskCompletionSource<object> sharedTcs;
			UniTaskCompletionSource<object> callerTcs = null;
			bool isNewAwaiter = false;

			lock (_lock)
			{
				if (_readyServices.TryGetValue(serviceType, out var serviceInfo))
				{
					return serviceInfo.Service;
				}

				if (!_serviceAwaiters.TryGetValue(serviceType, out sharedTcs))
				{
					sharedTcs = new UniTaskCompletionSource<object>();
					_serviceAwaiters[serviceType] = sharedTcs;
					isNewAwaiter = true;
				}
				
				// Create a separate TCS for this specific caller to handle individual cancellation
				if (cancellationToken.CanBeCanceled)
				{
					callerTcs = new UniTaskCompletionSource<object>();
				}
			}


			// If we have a cancellation token, use a separate TCS for this caller
			if (callerTcs != null)
			{
				// Set up forwarding from shared TCS to caller TCS
				_ = ForwardTaskResult(sharedTcs, callerTcs);
				
				// Set up cancellation for this specific caller
				using (cancellationToken.Register(() => callerTcs.TrySetCanceled()))
				{
					return await callerTcs.Task;
				}
			}
			else
			{
				// No cancellation token, just wait on the shared TCS
				return await sharedTcs.Task;
			}
		}
#else
		public async Task<T> GetServiceAsync<T>(CancellationToken cancellationToken = default) where T : class
		{
			var service = await GetServiceAsync(typeof(T), cancellationToken);
			return service as T;
		}

		public async Task<object> GetServiceAsync(Type serviceType, CancellationToken cancellationToken = default)
		{
			TaskCompletionSource<object> tcs;

			lock (_lock)
			{
				if (_readyServices.TryGetValue(serviceType, out var serviceInfo))
				{
					return serviceInfo.Service;
				}

				if (!_serviceAwaiters.TryGetValue(serviceType, out tcs))
				{
					tcs = new TaskCompletionSource<object>();
					_serviceAwaiters[serviceType] = tcs;
				}
			}

			// IMPORTANT: We cannot use cancellationToken.Register to cancel the shared TaskCompletionSource
			// because multiple callers might be waiting on the same TCS.
			// Instead, we create a linked source that combines the shared task with the cancellation token.
			
			// Create a new TCS for this specific caller
			var callerTcs = new TaskCompletionSource<object>();
			
			// When the shared task completes, complete the caller's task
			tcs.Task.ContinueWith(t =>
			{
				if (t.IsCompletedSuccessfully)
					callerTcs.TrySetResult(t.Result);
				else if (t.IsCanceled)
					callerTcs.TrySetCanceled();
				else if (t.IsFaulted)
					callerTcs.TrySetException(t.Exception.InnerExceptions);
			}, TaskContinuationOptions.ExecuteSynchronously);
			
			// If the cancellation token is cancelled, cancel only this caller's task
			using (cancellationToken.Register(() => callerTcs.TrySetCanceled()))
			{
				return await callerTcs.Task;
			}
		}
#endif

		public IServiceInjectionBuilder InjectServicesAsync(object target)
		{
			return new ServiceInjectionBuilder(this, target);
		}

		public void UnregisterService<T>() where T : class
		{
			UnregisterService(typeof(T));
		}

		public void UnregisterService(Type serviceType)
		{
			lock (_lock)
			{
				var removed = _readyServices.Remove(serviceType) || _registeredServices.Remove(serviceType);

				ServiceDependencyGraph.RemoveCircularDependencyExemption(serviceType);

#if UNITY_EDITOR
				if (removed && ServiceKitSettings.Instance.DebugLogging)
				{
					Debug.Log($"[ServiceKit] Unregistered {serviceType.Name}");
				}
#endif

				if (_serviceAwaiters.TryGetValue(serviceType, out var tcs))
				{
					_serviceAwaiters.Remove(serviceType);
					tcs.TrySetCanceled();
				}
			}
		}

		public string GetServiceStatus<T>() where T : class
		{
			return GetServiceStatus(typeof(T));
		}

		public string GetServiceStatus(Type serviceType)
		{
			lock (_lock)
			{
				if (_readyServices.ContainsKey(serviceType))
				{
					return "Ready";
				}

				if (_registeredServices.TryGetValue(serviceType, out var info))
				{
#if UNITY_EDITOR
					var waitTime = (DateTime.Now - info.RegisteredAt).TotalSeconds;
					return $"Registered (waiting {waitTime:F1}s)";
#else
					return "Registered";
#endif
				}

				return "Not registered";
			}
		}

		public IReadOnlyList<ServiceInfo> GetAllServices()
		{
			lock (_lock)
			{
				var allServices = ServiceKitObjectPool.RentServiceInfoList();
				try
				{
					foreach (var kvp in _readyServices)
					{
#if UNITY_EDITOR
						kvp.Value.DebugData.State = "Ready";
#endif
						allServices.Add(kvp.Value);
					}

					foreach (var kvp in _registeredServices)
					{
#if UNITY_EDITOR
						kvp.Value.ServiceInfo.DebugData.State = "Registered";
#endif
						allServices.Add(kvp.Value.ServiceInfo);
					}

					var result = new List<ServiceInfo>(allServices);
					return result;
				}
				finally
				{
					ServiceKitObjectPool.ReturnServiceInfoList(allServices);
				}
			}
		}

		public IReadOnlyList<ServiceInfo> GetServicesInScene(string sceneName)
		{
			lock (_lock)
			{
				var services = ServiceKitObjectPool.RentServiceInfoList();
				try
				{
#if UNITY_EDITOR
					foreach (var kvp in _readyServices)
					{
						if (kvp.Value.DebugData.SceneName == sceneName)
						{
							services.Add(kvp.Value);
						}
					}
					
					foreach (var kvp in _registeredServices)
					{
						if (kvp.Value.ServiceInfo.DebugData.SceneName == sceneName)
						{
							services.Add(kvp.Value.ServiceInfo);
						}
					}
#endif
					var result = new List<ServiceInfo>(services);
					return result;
				}
				finally
				{
					ServiceKitObjectPool.ReturnServiceInfoList(services);
				}
			}
		}

		public IReadOnlyList<ServiceInfo> GetDontDestroyOnLoadServices()
		{
			lock (_lock)
			{
				var services = ServiceKitObjectPool.RentServiceInfoList();
				try
				{
#if UNITY_EDITOR
					foreach (var kvp in _readyServices)
					{
						if (kvp.Value.DebugData.IsDontDestroyOnLoad)
						{
							services.Add(kvp.Value);
						}
					}
					
					foreach (var kvp in _registeredServices)
					{
						if (kvp.Value.ServiceInfo.DebugData.IsDontDestroyOnLoad)
						{
							services.Add(kvp.Value.ServiceInfo);
						}
					}
#endif
					var result = new List<ServiceInfo>(services);
					return result;
				}
				finally
				{
					ServiceKitObjectPool.ReturnServiceInfoList(services);
				}
			}
		}

		public void UnregisterServicesFromScene(Scene scene)
		{
			lock (_lock)
			{
				var servicesToRemove = new List<Type>();

				foreach (var kvp in _readyServices)
				{
					var serviceInfo = kvp.Value;

					if (!(serviceInfo.Service is MonoBehaviour))
						continue;

#if UNITY_EDITOR
					if (serviceInfo.DebugData.IsDontDestroyOnLoad)
						continue;

					if (serviceInfo.DebugData.SceneHandle == scene.handle)
					{
						servicesToRemove.Add(kvp.Key);
					}
					else
#endif
					if (serviceInfo.Service is MonoBehaviour mb && mb == null)
					{
						servicesToRemove.Add(kvp.Key);
					}
				}

				foreach (var kvp in _registeredServices)
				{
					var serviceInfo = kvp.Value.ServiceInfo;

					if (!(serviceInfo.Service is MonoBehaviour))
						continue;

#if UNITY_EDITOR
					if (serviceInfo.DebugData.IsDontDestroyOnLoad)
						continue;

					if (serviceInfo.DebugData.SceneHandle == scene.handle)
					{
						servicesToRemove.Add(kvp.Key);
					}
					else
#endif
					if (serviceInfo.Service is MonoBehaviour mb && mb == null)
					{
						servicesToRemove.Add(kvp.Key);
					}
				}

				foreach (var type in servicesToRemove)
				{
					_readyServices.Remove(type);
					_registeredServices.Remove(type);


					if (_serviceAwaiters.TryGetValue(type, out var tcs))
					{
						tcs.TrySetCanceled();
						_serviceAwaiters.Remove(type);
					}
				}

				_trackedScenes.Remove(scene);
			}
		}

		public void CleanupDestroyedServices()
		{
			lock (_lock)
			{
				var servicesToRemove = new List<Type>();

				foreach (var kvp in _readyServices)
				{
					if (kvp.Value.Service is MonoBehaviour mb && mb == null)
					{
						servicesToRemove.Add(kvp.Key);
					}
				}

				foreach (var kvp in _registeredServices)
				{
					if (kvp.Value.ServiceInfo.Service is MonoBehaviour mb && mb == null)
					{
						servicesToRemove.Add(kvp.Key);
					}
				}

				foreach (var type in servicesToRemove)
				{
					_readyServices.Remove(type);
					_registeredServices.Remove(type);

				}
			}
		}

		public void ClearServices()
		{
			lock (_lock)
			{
				// Cancel all pending service awaiters first
				foreach (var kvp in _serviceAwaiters)
				{
					try
					{
						kvp.Value.TrySetCanceled();
					}
					catch
					{
						// Ignore any exceptions during cleanup
					}
				}
				_serviceAwaiters.Clear();
				
				_readyServices.Clear();
				_registeredServices.Clear();
				_trackedScenes.Clear();

				// Clear dependency graph and exemptions
				ServiceDependencyGraph.ClearAll();
			}
		}

		private static ServiceInfo CreateServiceInfo(object service, Type type, string registeredBy, ServiceTag[] tags = null)
		{
			var info = new ServiceInfo
			{
				Service = service,
				ServiceType = type
			};

			if (tags != null && tags.Length > 0)
			{
				info.Tags.AddRange(tags);
			}

#if UNITY_EDITOR
			info.DebugData.RegisteredAt = DateTime.Now;
			info.DebugData.RegisteredBy = registeredBy ?? "Unknown";

			if (service is MonoBehaviour monoBehaviour && monoBehaviour != null)
			{
				var scene = monoBehaviour.gameObject.scene;
				info.DebugData.SceneName = scene.name;
				info.DebugData.SceneHandle = scene.handle;

				// Check if the object is in the DontDestroyOnLoad scene
				// Note: Both DontDestroyOnLoad and addressable scenes have buildIndex == -1,
				// so we must check the actual scene name to differentiate them
				if (scene.name == "DontDestroyOnLoad")
				{
					info.DebugData.IsDontDestroyOnLoad = true;
				}
				else
				{
					// Normal scene object (including addressable scenes)
					info.DebugData.IsDontDestroyOnLoad = false;
				}
			}
			else
			{
				info.DebugData.SceneName = "Non-MonoBehaviour";
				info.DebugData.SceneHandle = -1;
			}
#endif

			return info;
		}

		private static void ValidateServiceNotNull<T>(T service, string registeredBy) where T : class
		{
			if (service != null) return;

			var serviceType = typeof(T);
			var callerType = registeredBy != null ? GetCallerTypeFromStackTrace() : null;

			if (serviceType.IsInterface && callerType != null)
			{
				ThrowDetailedInterfaceImplementationError(serviceType, callerType);
			}

			throw new ArgumentNullException(nameof(service), 
				$"Service registration failed for type '{serviceType.Name}'. The service object cannot be null.");
		}

		private static void ThrowDetailedInterfaceImplementationError(Type serviceType, Type callerType)
		{
			var sb = ServiceKitObjectPool.RentStringBuilder();
			try
			{
				sb.Append("Service registration failed for type '");
				sb.Append(serviceType.Name);
				sb.Append("'. The service object is null, which often occurs when a ServiceKitBehaviour<");
				sb.Append(serviceType.Name);
				sb.Append("> does not implement the interface '");
				sb.Append(serviceType.Name);
				sb.Append("'. Caller type: '");
				sb.Append(callerType.Name);
				sb.Append("' Implements interfaces: [");
				
				var interfaces = callerType.GetInterfaces();
				for (int i = 0; i < interfaces.Length; i++)
				{
					if (i > 0) sb.Append(", ");
					sb.Append(interfaces[i].Name);
				}
				
				sb.Append("]. Please ensure that '");
				sb.Append(callerType.Name);
				sb.Append("' implements '");
				sb.Append(serviceType.Name);
				sb.Append("'.");
				
				throw new InvalidOperationException(sb.ToString());
			}
			finally
			{
				ServiceKitObjectPool.ReturnStringBuilder(sb);
			}
		}

		private void TrackServiceScene(MonoBehaviour monoBehaviour)
		{
			var scene = monoBehaviour.gameObject.scene;
			if (scene.IsValid() && !_trackedScenes.Contains(scene))
			{
				_trackedScenes.Add(scene);
			}
		}

		private void CompleteAwaiters(Type type, object service)
		{
#if SERVICEKIT_UNITASK
			UniTaskCompletionSource<object> tcs;
#else
			TaskCompletionSource<object> tcs;
#endif
			lock (_lock)
			{
				if (_serviceAwaiters.TryGetValue(type, out tcs))
				{
					_serviceAwaiters.Remove(type);
				}
			}
			tcs?.TrySetResult(service);
		}

		private static void CheckWaitingServices(Type newlyReadyType)
		{
			// Placeholder: could track dependent registrations and signal here.
		}

		private void OnSceneUnloaded(Scene scene)
		{
			if (ServiceKitSettings.Instance.AutoCleanupOnSceneUnload)
			{
				UnregisterServicesFromScene(scene);
			}
		}

		private void OnEnable()
		{
			SceneManager.sceneUnloaded += OnSceneUnloaded;
		}

		private void OnDisable()
		{
			SceneManager.sceneUnloaded -= OnSceneUnloaded;
		}

		private void OnDestroy()
		{
			// Clean up timeout manager when the locator is destroyed
#if UNITY_EDITOR
			if (!Application.isPlaying)
			{
				ServiceKitTimeoutManager.Cleanup();
			}
#endif
			ClearServices();
		}

		private List<FieldInfo> GetInjectableFields(Type serviceType)
		{
			var fields = new List<FieldInfo>();
			var currentType = serviceType;

			while (currentType != null && currentType != typeof(object))
			{
				var typeFields = currentType
					.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)
					.Where(f => f.GetCustomAttribute<InjectServiceAttribute>() != null);

				fields.AddRange(typeFields);
				currentType = currentType.BaseType;
			}

			return fields;
		}

		private Type FindTypeByName(string typeName)
		{
			foreach (var registeredType in _registeredServices.Keys)
			{
				if (registeredType.Name == typeName) return registeredType;
			}
			
			foreach (var readyType in _readyServices.Keys)
			{
				if (readyType.Name == typeName) return readyType;
			}
			return null;
		}

		private void MarkCircularDependencyChainAsError(string circularDependency)
		{
			var typesInPath = circularDependency.Split(new[] { " → " }, StringSplitOptions.None);
			foreach (var typeName in typesInPath)
			{
				var trimmedTypeName = typeName.Trim();
				var foundType = FindTypeByName(trimmedTypeName);
				if (foundType != null)
				{
					ServiceDependencyGraph.AddCircularDependencyError(foundType);
				}
			}
		}

		private static Type GetCallerTypeFromStackTrace()
		{
			try
			{
				var stackTrace = new System.Diagnostics.StackTrace();
				
				// Skip frames: GetCallerTypeFromStackTrace, RegisterService, and public RegisterService wrapper
				for (var i = 3; i < stackTrace.FrameCount; i++)
				{
					var method = stackTrace.GetFrame(i)?.GetMethod();
					if (method != null && method.DeclaringType != null)
					{
						// Skip ServiceKit internal types
						if (!method.DeclaringType.Namespace?.StartsWith("Nonatomic.ServiceKit") ?? false)
						{
							return method.DeclaringType;
						}
						
						// Check if it's a ServiceKitBehaviour derived type
						if (method.DeclaringType.IsSubclassOf(typeof(MonoBehaviour)) && 
							method.DeclaringType.Name.Contains("ServiceKitBehaviour"))
						{
							return method.DeclaringType;
						}
					}
				}
			}
			catch
			{
				// Silently fail if we can't get stack trace
			}
			return null;
		}

#region Tag Management

		public void AddTagsToService<T>(params ServiceTag[] tags) where T : class
		{
			AddTagsToService(typeof(T), tags);
		}

		public void AddTagsToService(Type serviceType, params ServiceTag[] tags)
		{
			if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
			if (tags == null || tags.Length == 0) return;

			lock (_lock)
			{
				ServiceInfo serviceInfo = null;

				if (_readyServices.TryGetValue(serviceType, out var readyInfo))
				{
					serviceInfo = readyInfo;
				}
				else if (_registeredServices.TryGetValue(serviceType, out var registeredInfo))
				{
					serviceInfo = registeredInfo.ServiceInfo;
				}

				if (serviceInfo == null) return;

				foreach (var tag in tags)
				{
					if (!string.IsNullOrWhiteSpace(tag.name) && !serviceInfo.Tags.Any(t => t.name == tag.name))
					{
						serviceInfo.Tags.Add(tag);
					}
				}
			}
		}

		public void RemoveTagsFromService<T>(params string[] tags) where T : class
		{
			RemoveTagsFromService(typeof(T), tags);
		}

		public void RemoveTagsFromService(Type serviceType, params string[] tags)
		{
			if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
			if (tags == null || tags.Length == 0) return;

			lock (_lock)
			{
				ServiceInfo serviceInfo = null;

				if (_readyServices.TryGetValue(serviceType, out var readyInfo))
				{
					serviceInfo = readyInfo;
				}
				else if (_registeredServices.TryGetValue(serviceType, out var registeredInfo))
				{
					serviceInfo = registeredInfo.ServiceInfo;
				}

				if (serviceInfo == null) return;

				foreach (var tagName in tags)
				{
					serviceInfo.Tags.RemoveAll(t => t.name == tagName);
				}
			}
		}

		public IReadOnlyList<string> GetServiceTags<T>() where T : class
		{
			return GetServiceTags(typeof(T));
		}

		public IReadOnlyList<string> GetServiceTags(Type serviceType)
		{
			if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));

			lock (_lock)
			{
				ServiceInfo serviceInfo = null;

				if (_readyServices.TryGetValue(serviceType, out var readyInfo))
				{
					serviceInfo = readyInfo;
				}
				else if (_registeredServices.TryGetValue(serviceType, out var registeredInfo))
				{
					serviceInfo = registeredInfo.ServiceInfo;
				}

				if (serviceInfo?.Tags == null || serviceInfo.Tags.Count == 0)
				{
					return new List<string>().AsReadOnly();
				}

				var tagNames = new List<string>(serviceInfo.Tags.Count);
				foreach (var tag in serviceInfo.Tags)
				{
					tagNames.Add(tag.name);
				}
				return tagNames.AsReadOnly();
			}
		}

		public IReadOnlyList<ServiceInfo> GetServicesWithTag(string tag)
		{
			if (string.IsNullOrWhiteSpace(tag)) return new List<ServiceInfo>().AsReadOnly();

			lock (_lock)
			{
				var result = new List<ServiceInfo>();

				foreach (var kvp in _readyServices)
				{
					if (kvp.Value.Tags.Any(t => t.name == tag))
					{
						result.Add(kvp.Value);
					}
				}

				foreach (var kvp in _registeredServices)
				{
					if (kvp.Value.ServiceInfo.Tags.Any(t => t.name == tag) && !result.Contains(kvp.Value.ServiceInfo))
					{
						result.Add(kvp.Value.ServiceInfo);
					}
				}

				return result.AsReadOnly();
			}
		}

		public IReadOnlyList<ServiceInfo> GetServicesWithAnyTag(params string[] tags)
		{
			if (tags == null || tags.Length == 0) return new List<ServiceInfo>().AsReadOnly();

			lock (_lock)
			{
				var result = ServiceKitObjectPool.RentServiceInfoList();
				try
				{
					var hasValidTags = false;
					for (int i = 0; i < tags.Length; i++)
					{
						if (!string.IsNullOrWhiteSpace(tags[i]))
						{
							hasValidTags = true;
							break;
						}
					}
					
					if (!hasValidTags) return new List<ServiceInfo>().AsReadOnly();

					foreach (var kvp in _readyServices)
					{
						if (HasAnyTag(kvp.Value.Tags, tags))
						{
							result.Add(kvp.Value);
						}
					}

					foreach (var kvp in _registeredServices)
					{
						if (HasAnyTag(kvp.Value.ServiceInfo.Tags, tags) && !result.Contains(kvp.Value.ServiceInfo))
						{
							result.Add(kvp.Value.ServiceInfo);
						}
					}

					var finalResult = new List<ServiceInfo>(result);
					return finalResult.AsReadOnly();
				}
				finally
				{
					ServiceKitObjectPool.ReturnServiceInfoList(result);
				}
			}
		}

		private static bool HasAnyTag(List<ServiceTag> serviceTags, string[] searchTags)
		{
			if (serviceTags == null || serviceTags.Count == 0) return false;
			
			for (int i = 0; i < serviceTags.Count; i++)
			{
				for (int j = 0; j < searchTags.Length; j++)
				{
					if (!string.IsNullOrWhiteSpace(searchTags[j]) && serviceTags[i].name == searchTags[j])
					{
						return true;
					}
				}
			}
			return false;
		}

		public IReadOnlyList<ServiceInfo> GetServicesWithAllTags(params string[] tags)
		{
			if (tags == null || tags.Length == 0) return new List<ServiceInfo>().AsReadOnly();

			lock (_lock)
			{
				var result = new List<ServiceInfo>();
				var validTags = tags.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
				if (validTags.Count == 0) return result.AsReadOnly();

				foreach (var kvp in _readyServices)
				{
					if (validTags.All(tagName => kvp.Value.Tags.Any(serviceTag => serviceTag.name == tagName)))
					{
						result.Add(kvp.Value);
					}
				}

				foreach (var kvp in _registeredServices)
				{
					if (validTags.All(tagName => kvp.Value.ServiceInfo.Tags.Any(serviceTag => serviceTag.name == tagName)) && !result.Contains(kvp.Value.ServiceInfo))
					{
						result.Add(kvp.Value.ServiceInfo);
					}
				}

				return result.AsReadOnly();
			}
		}

#endregion
	}
}
