using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nonatomic.ServiceKit
{
	[CreateAssetMenu(fileName = "ServiceKit", menuName = "ServiceKit/ServiceKitLocator")]
	public class ServiceKitLocator : ScriptableObject, IServiceKitLocator
	{
		private readonly Dictionary<Type, ServiceInfo> _readyServices = new Dictionary<Type, ServiceInfo>();
		private readonly Dictionary<Type, RegisteredServiceInfo> _registeredServices = new Dictionary<Type, RegisteredServiceInfo>();
		private readonly Dictionary<Type, TaskCompletionSource<object>> _serviceAwaiters = new Dictionary<Type, TaskCompletionSource<object>>();
		private readonly object _lock = new object();
		private readonly HashSet<Scene> _trackedScenes = new HashSet<Scene>();

		private class RegisteredServiceInfo
		{
			public ServiceInfo ServiceInfo { get; set; }
			public DateTime RegisteredAt { get; set; }
			public List<Type> WaitingForDependencies { get; set; } = new List<Type>();
		}

		/// <summary>
		/// Register a service instance (Phase 1) - Service is NOT available for injection yet
		/// </summary>
		public void RegisterService<T>(T service, [CallerMemberName] string registeredBy = null) where T : class
		{
			RegisterService(service, false, registeredBy);
		}

		/// <summary>
		/// Register a service with circular dependency exemption (Phase 1)
		/// Use this for immutable third-party services that cannot have dependencies on your project
		/// </summary>
		public void RegisterServiceWithCircularExemption<T>(T service, [CallerMemberName] string registeredBy = null) where T : class
		{
			RegisterService(service, true, registeredBy);
		}

		private void RegisterService<T>(T service, bool exemptFromCircularDependencyCheck, [CallerMemberName] string registeredBy = null) where T : class
		{
			if (service == null) throw new ArgumentNullException(nameof(service));

			lock (_lock)
			{
				var type = typeof(T);
				var serviceInfo = CreateServiceInfo(service, type, registeredBy);

				// Check if already exists
				if (_readyServices.ContainsKey(type))
				{
					if (ServiceKitSettings.Instance.DebugLogging)
					{
						Debug.LogWarning($"[ServiceKit] Service {type.Name} is already ready. Use UnregisterService first.");
					}
					
					return;
				}

				if (_registeredServices.ContainsKey(type))
				{
					if (ServiceKitSettings.Instance.DebugLogging)
					{
						Debug.LogWarning($"[ServiceKit] Service {type.Name} is already registered. Updating registration.");
					}
				}

				// Track circular dependency exemption
				if (exemptFromCircularDependencyCheck)
				{
					ServiceInjectionBuilder.AddCircularDependencyExemption(type);
					
					if (ServiceKitSettings.Instance.DebugLogging)
					{
						Debug.Log($"[ServiceKit] Service {type.Name} is exempt from circular dependency checks");
					}
				}

				// Analyze dependencies and check for circular dependencies at registration time
				if (!exemptFromCircularDependencyCheck)
				{
					var fieldsToInject = GetInjectableFields(service.GetType());
					ServiceInjectionBuilder.UpdateDependencyGraphForRegistration(type, fieldsToInject);
					
					var circularDependency = ServiceInjectionBuilder.DetectCircularDependencyAtRegistration(type);
					if (circularDependency != null)
					{
						ServiceInjectionBuilder.AddCircularDependencyError(type);
						
						// Mark all services in the circular dependency chain as having errors
						var typesInPath = circularDependency.Split(new[] { " → " }, StringSplitOptions.None);
						foreach (var typeName in typesInPath)
						{
							var trimmedTypeName = typeName.Trim();
							// Find the type and mark it as having an error
							var foundType = FindTypeByName(trimmedTypeName);
							if (foundType != null)
							{
								ServiceInjectionBuilder.AddCircularDependencyError(foundType);
							}
						}
						
						if (ServiceKitSettings.Instance.DebugLogging)
						{
							Debug.LogError($"[ServiceKit] Circular dependency detected during registration: {circularDependency}");
						}
					}
				}

				// Track the scene if it's a MonoBehaviour
				if (service is MonoBehaviour monoBehaviour && monoBehaviour != null)
				{
					TrackServiceScene(monoBehaviour);
				}

				// Add to registered services
				_registeredServices[type] = new RegisteredServiceInfo
				{
					ServiceInfo = serviceInfo,
					RegisteredAt = DateTime.Now
				};

				if (ServiceKitSettings.Instance.DebugLogging)
				{
					Debug.Log($"[ServiceKit] Registered {type.Name} (not ready yet) from scene '{serviceInfo.SceneName}' by {registeredBy}");
				}
			}
		}

		/// <summary>
		/// Mark a service as ready (Phase 3) - Service becomes available for injection
		/// </summary>
		public void ReadyService<T>() where T : class
		{
			ReadyService(typeof(T));
		}

		/// <summary>
		/// Mark a service as ready by type (Phase 3)
		/// </summary>
		public void ReadyService(Type serviceType)
		{
			RegisteredServiceInfo registeredInfo;
			
			lock (_lock)
			{
				if (!_registeredServices.TryGetValue(serviceType, out registeredInfo))
				{
					throw new InvalidOperationException(
						$"Cannot ready service {serviceType.Name} - it must be registered first");
				}

				if (_readyServices.ContainsKey(serviceType))
				{
					if (ServiceKitSettings.Instance.DebugLogging)
						Debug.LogWarning($"Service {serviceType.Name} is already ready");
					return;
				}

				// Move from registered to ready
				_registeredServices.Remove(serviceType);
				_readyServices[serviceType] = registeredInfo.ServiceInfo;

				if (ServiceKitSettings.Instance.DebugLogging)
				{
					var initTime = (DateTime.Now - registeredInfo.RegisteredAt).TotalMilliseconds;
					Debug.Log($"[ServiceKit] Service {serviceType.Name} is now READY (init took {initTime:F0}ms)");
				}
			}

			// Complete any awaiters
			CompleteAwaiters(serviceType, registeredInfo.ServiceInfo.Service);
			
			// Check if any registered services were waiting for this
			CheckWaitingServices(serviceType);
		}

		/// <summary>
		/// Register and immediately ready a service (for services without dependencies/initialization)
		/// </summary>
		public void RegisterAndReadyService<T>(T service, [CallerMemberName] string registeredBy = null) where T : class
		{
			RegisterService(service, false, registeredBy);
			ReadyService<T>();
		}

		/// <summary>
		/// Register and immediately ready a service with circular dependency exemption
		/// </summary>
		public void RegisterAndReadyServiceWithCircularExemption<T>(T service, [CallerMemberName] string registeredBy = null) where T : class
		{
			RegisterService(service, true, registeredBy);
			ReadyService<T>();
		}

		/// <summary>
		/// Get a service synchronously (returns null if not ready)
		/// </summary>
		public T GetService<T>() where T : class
		{
			return GetService(typeof(T)) as T;
		}

		/// <summary>
		/// Get a service synchronously by type (returns null if not ready)
		/// </summary>
		public object GetService(Type serviceType)
		{
			lock (_lock)
			{
				return _readyServices.TryGetValue(serviceType, out var serviceInfo)
					? serviceInfo.Service
					: null;
			}
		}

		/// <summary>
		/// Check if a service is registered (but maybe not ready)
		/// </summary>
		public bool IsServiceRegistered<T>() where T : class
		{
			return IsServiceRegistered(typeof(T));
		}

		/// <summary>
		/// Check if a service is registered by type
		/// </summary>
		public bool IsServiceRegistered(Type serviceType)
		{
			lock (_lock)
			{
				return _registeredServices.ContainsKey(serviceType) || _readyServices.ContainsKey(serviceType);
			}
		}

		/// <summary>
		/// Check if a service is ready
		/// </summary>
		public bool IsServiceReady<T>() where T : class
		{
			return IsServiceReady(typeof(T));
		}

		/// <summary>
		/// Check if a service is ready by type
		/// </summary>
		public bool IsServiceReady(Type serviceType)
		{
			lock (_lock)
			{
				return _readyServices.ContainsKey(serviceType);
			}
		}

		/// <summary>
		/// Check if a service is exempt from circular dependency checks
		/// </summary>
		public bool IsServiceCircularDependencyExempt<T>() where T : class
		{
			return IsServiceCircularDependencyExempt(typeof(T));
		}

		/// <summary>
		/// Check if a service is exempt from circular dependency checks by type
		/// </summary>
		public bool IsServiceCircularDependencyExempt(Type serviceType)
		{
			return ServiceInjectionBuilder.IsCircularDependencyExempt(serviceType);
		}

		/// <summary>
		/// Try to get a service synchronously
		/// </summary>
		public bool TryGetService<T>(out T service) where T : class
		{
			if (TryGetService(typeof(T), out object serviceObj))
			{
				service = serviceObj as T;
				return service != null;
			}

			service = null;
			return false;
		}

		/// <summary>
		/// Try to get a service synchronously by type
		/// </summary>
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

		/// <summary>
		/// Get a service asynchronously (waits until ready)
		/// </summary>
		public async Task<T> GetServiceAsync<T>(CancellationToken cancellationToken = default) where T : class
		{
			object service = await GetServiceAsync(typeof(T), cancellationToken);
			return service as T;
		}

		/// <summary>
		/// Get a service asynchronously by type (waits until ready)
		/// </summary>
		public async Task<object> GetServiceAsync(Type serviceType, CancellationToken cancellationToken = default)
		{
			TaskCompletionSource<object> tcs;

			lock (_lock)
			{
				// Check if already ready
				if (_readyServices.TryGetValue(serviceType, out var serviceInfo))
				{
					return serviceInfo.Service;
				}

				// Track that someone is waiting for this service
				if (_registeredServices.TryGetValue(serviceType, out var registeredInfo))
				{
					// Service is registered but not ready - good case for waiting
				}
				else
				{
					// Service not even registered - still wait, it might register later
					if (ServiceKitSettings.Instance.DebugLogging)
					{
						Debug.LogWarning($"[ServiceKit] Waiting for service {serviceType.Name} that isn't registered yet");
					}
				}

				if (!_serviceAwaiters.TryGetValue(serviceType, out tcs))
				{
					tcs = new TaskCompletionSource<object>();
					_serviceAwaiters[serviceType] = tcs;
				}
			}

			using (cancellationToken.Register(() => tcs.TrySetCanceled()))
			{
				return await tcs.Task;
			}
		}

		/// <summary>
		/// Start fluent injection for an object
		/// </summary>
		public IServiceInjectionBuilder InjectServicesAsync(object target)
		{
			return new ServiceInjectionBuilder(this, target);
		}

		/// <summary>
		/// Unregister a service (removes from both registered and ready)
		/// </summary>
		public void UnregisterService<T>() where T : class
		{
			UnregisterService(typeof(T));
		}

		/// <summary>
		/// Unregister a service by type
		/// </summary>
		public void UnregisterService(Type serviceType)
		{
			lock (_lock)
			{
				var removed = _readyServices.Remove(serviceType) || _registeredServices.Remove(serviceType);
				
				// Also remove from exemptions
				ServiceInjectionBuilder.RemoveCircularDependencyExemption(serviceType);
				
				if (removed && ServiceKitSettings.Instance.DebugLogging)
				{
					Debug.Log($"[ServiceKit] Unregistered {serviceType.Name}");
				}
				
				// Cancel any awaiters
				if (_serviceAwaiters.TryGetValue(serviceType, out var tcs))
				{
					_serviceAwaiters.Remove(serviceType);
					tcs.TrySetCanceled();
				}
			}
		}

		/// <summary>
		/// Get debug information about a service
		/// </summary>
		public string GetServiceStatus<T>() where T : class
		{
			return GetServiceStatus(typeof(T));
		}

		/// <summary>
		/// Get debug information about a service by type
		/// </summary>
		public string GetServiceStatus(Type serviceType)
		{
			lock (_lock)
			{
				if (_readyServices.ContainsKey(serviceType))
					return "Ready";
				
				if (_registeredServices.TryGetValue(serviceType, out var info))
				{
					var waitTime = (DateTime.Now - info.RegisteredAt).TotalSeconds;
					return $"Registered (waiting {waitTime:F1}s)";
				}
				
				return "Not registered";
			}
		}

		/// <summary>
		/// Get all services with their current status
		/// </summary>
		public IReadOnlyList<ServiceInfo> GetAllServices()
		{
			lock (_lock)
			{
				var allServices = new List<ServiceInfo>();
				
				// Add ready services
				foreach (var kvp in _readyServices)
				{
					kvp.Value.State = "Ready";
					allServices.Add(kvp.Value);
				}
				
				// Add registered but not ready services
				foreach (var kvp in _registeredServices)
				{
					kvp.Value.ServiceInfo.State = "Registered";
					allServices.Add(kvp.Value.ServiceInfo);
				}
				
				return allServices;
			}
		}

		/// <summary>
		/// Get services filtered by scene
		/// </summary>
		public IReadOnlyList<ServiceInfo> GetServicesInScene(string sceneName)
		{
			lock (_lock)
			{
				var services = new List<ServiceInfo>();
				
				// Add ready services in scene
				services.AddRange(_readyServices.Values.Where(s => s.SceneName == sceneName));
				
				// Add registered services in scene
				services.AddRange(_registeredServices.Values
					.Where(r => r.ServiceInfo.SceneName == sceneName)
					.Select(r => r.ServiceInfo));
				
				return services;
			}
		}

		/// <summary>
		/// Get services marked as DontDestroyOnLoad
		/// </summary>
		public IReadOnlyList<ServiceInfo> GetDontDestroyOnLoadServices()
		{
			lock (_lock)
			{
				var services = new List<ServiceInfo>();
				
				// Add ready DontDestroyOnLoad services
				services.AddRange(_readyServices.Values.Where(s => s.IsDontDestroyOnLoad));
				
				// Add registered DontDestroyOnLoad services
				services.AddRange(_registeredServices.Values
					.Where(r => r.ServiceInfo.IsDontDestroyOnLoad)
					.Select(r => r.ServiceInfo));
				
				return services;
			}
		}

		/// <summary>
		/// Unregister all services from a specific scene
		/// </summary>
		public void UnregisterServicesFromScene(Scene scene)
		{
			lock (_lock)
			{
				var servicesToRemove = new List<Type>();

				// Check ready services
				foreach (var kvp in _readyServices)
				{
					var serviceInfo = kvp.Value;

					// Skip non-MonoBehaviour services
					if (!(serviceInfo.Service is MonoBehaviour))
						continue;

					// Skip DontDestroyOnLoad services
					if (serviceInfo.IsDontDestroyOnLoad)
						continue;

					// Check if service belongs to the unloaded scene
					if (serviceInfo.SceneHandle == scene.handle)
					{
						servicesToRemove.Add(kvp.Key);
					}
					// Also check if the MonoBehaviour has been destroyed
					else if (serviceInfo.Service is MonoBehaviour mb && mb == null)
					{
						servicesToRemove.Add(kvp.Key);
					}
				}

				// Check registered services
				foreach (var kvp in _registeredServices)
				{
					var serviceInfo = kvp.Value.ServiceInfo;

					// Skip non-MonoBehaviour services
					if (!(serviceInfo.Service is MonoBehaviour))
						continue;

					// Skip DontDestroyOnLoad services
					if (serviceInfo.IsDontDestroyOnLoad)
						continue;

					// Check if service belongs to the unloaded scene
					if (serviceInfo.SceneHandle == scene.handle)
					{
						servicesToRemove.Add(kvp.Key);
					}
					// Also check if the MonoBehaviour has been destroyed
					else if (serviceInfo.Service is MonoBehaviour mb && mb == null)
					{
						servicesToRemove.Add(kvp.Key);
					}
				}

				// Remove identified services
				foreach (var type in servicesToRemove)
				{
					_readyServices.Remove(type);
					_registeredServices.Remove(type);

					if (ServiceKitSettings.Instance.DebugLogging)
					{
						Debug.Log($"[ServiceKit] Auto-unregistered {type.Name} from unloaded scene '{scene.name}'");
					}

					// Cancel any pending requests for this service
					if (_serviceAwaiters.TryGetValue(type, out var tcs))
					{
						tcs.TrySetCanceled();
						_serviceAwaiters.Remove(type);
					}
				}

				_trackedScenes.Remove(scene);
			}
		}

		/// <summary>
		/// Manually clean up destroyed MonoBehaviour services
		/// </summary>
		public void CleanupDestroyedServices()
		{
			lock (_lock)
			{
				var servicesToRemove = new List<Type>();

				// Check ready services
				foreach (var kvp in _readyServices)
				{
					if (kvp.Value.Service is MonoBehaviour mb && mb == null)
					{
						servicesToRemove.Add(kvp.Key);
					}
				}

				// Check registered services
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

					if (ServiceKitSettings.Instance.DebugLogging)
					{
						Debug.Log($"[ServiceKit] Cleaned up destroyed service: {type.Name}");
					}
				}
			}
		}

		/// <summary>
		/// Clear all registered services
		/// </summary>
		public void ClearServices()
		{
			lock (_lock)
			{
				_readyServices.Clear();
				_registeredServices.Clear();
				_trackedScenes.Clear();

				// Cancel all pending service requests
				foreach (var kvp in _serviceAwaiters)
				{
					kvp.Value.TrySetCanceled();
				}
				_serviceAwaiters.Clear();
				
				// Clear dependency graph and exemptions
				ServiceInjectionBuilder.ClearDependencyGraph();
			}
		}

		private ServiceInfo CreateServiceInfo(object service, Type type, string registeredBy)
		{
			var info = new ServiceInfo
			{
				Service = service,
				ServiceType = type,
				RegisteredAt = DateTime.Now,
				RegisteredBy = registeredBy ?? "Unknown"
			};

			// Determine scene information
			if (service is MonoBehaviour monoBehaviour && monoBehaviour != null)
			{
				var scene = monoBehaviour.gameObject.scene;
				info.SceneName = scene.name;
				info.SceneHandle = scene.handle;

				// Check if it's in DontDestroyOnLoad
				if (monoBehaviour.gameObject.scene.buildIndex == -1)
				{
					info.IsDontDestroyOnLoad = true;
					info.SceneName = "DontDestroyOnLoad";
				}
			}
			else
			{
				info.SceneName = "Non-MonoBehaviour";
				info.SceneHandle = -1;
			}

			return info;
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
			TaskCompletionSource<object> tcs;
			
			lock (_lock)
			{
				if (_serviceAwaiters.TryGetValue(type, out tcs))
				{
					_serviceAwaiters.Remove(type);
				}
			}
			
			tcs?.TrySetResult(service);
		}

		private void CheckWaitingServices(Type newlyReadyType)
		{
			// This could be extended to track which services are waiting for which dependencies
			// For now, we rely on ServiceInjectionBuilder to handle the waiting
			// In a future enhancement, we could maintain a dependency graph here
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
			ClearServices();
		}

		/// <summary>
		/// Get injectable fields from a service type (used for dependency analysis at registration time)
		/// </summary>
		private List<FieldInfo> GetInjectableFields(Type serviceType)
		{
			var fields = new List<FieldInfo>();
			var currentType = serviceType;

			// Walk up the inheritance hierarchy
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

		/// <summary>
		/// Find a type by its name (helper for circular dependency error marking)
		/// </summary>
		private Type FindTypeByName(string typeName)
		{
			// First check in registered services
			foreach (var registeredType in _registeredServices.Keys)
			{
				if (registeredType.Name == typeName)
					return registeredType;
			}

			// Then check in ready services
			foreach (var readyType in _readyServices.Keys)
			{
				if (readyType.Name == typeName)
					return readyType;
			}

			return null;
		}
	}
}