using System;
using System.Collections.Generic;
using System.Linq;
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
		private readonly Dictionary<Type, ServiceInfo> _services = new Dictionary<Type, ServiceInfo>();
		private readonly Dictionary<Type, TaskCompletionSource<object>> _pendingServices = new Dictionary<Type, TaskCompletionSource<object>>();
		private readonly object _lock = new object();
		private readonly HashSet<Scene> _trackedScenes = new HashSet<Scene>();

		/// <summary>
		/// Get all registered services with their information
		/// </summary>
		public IReadOnlyList<ServiceInfo> GetAllServices()
		{
			lock (_lock)
			{
				return _services.Values.ToList();
			}
		}

		/// <summary>
		/// Get services filtered by scene
		/// </summary>
		public IReadOnlyList<ServiceInfo> GetServicesInScene(string sceneName)
		{
		    lock (_lock)
		    {
			    return _services.Values
				    .Where(s => s.SceneName == sceneName)
				    .ToList();
		    }
		}

		/// <summary>
		/// Get services marked as DontDestroyOnLoad
		/// </summary>
		public IReadOnlyList<ServiceInfo> GetDontDestroyOnLoadServices()
		{
			lock (_lock)
			{
				return _services.Values
					.Where(s => s.IsDontDestroyOnLoad)
					.ToList();
			}
		}

		/// <summary>
		/// Register a service instance
		/// </summary>
		/// <summary>
		/// Register a service instance
		/// </summary>
		public void RegisterService<T>(T service, [CallerMemberName] string registeredBy = null) where T : class
		{
			lock (_lock)
			{
				var type = typeof(T);
				var serviceInfo = CreateServiceInfo(service, type, registeredBy);
				
				if (service == null) throw new ArgumentNullException(nameof(service));
				if (_services.ContainsKey(type) && ServiceKitSettings.Instance.DebugLogging) Debug.LogWarning($"Overwriting existing service for {type.Name}");

				_services[type] = serviceInfo;

				// Track the scene if it's a MonoBehaviour
				if (service is MonoBehaviour monoBehaviour && monoBehaviour != null)
				{
					TrackServiceScene(monoBehaviour);
				}

				// Complete any pending requests for this service
				if (_pendingServices.TryGetValue(type, out var tcs))
				{
					tcs.TrySetResult(service);
					_pendingServices.Remove(type);
				}

				if (ServiceKitSettings.Instance.DebugLogging)
				{
					Debug.Log($"[ServiceKit] Registered {type.Name} from scene '{serviceInfo.SceneName}' by {registeredBy}");
				}
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

				foreach (var kvp in _services)
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

				// Remove identified services
				foreach (var type in servicesToRemove)
				{
					_services.Remove(type);

					if (ServiceKitSettings.Instance.DebugLogging)
					{
						Debug.Log($"[ServiceKit] Auto-unregistered {type.Name} from unloaded scene '{scene.name}'");
					}

					// Cancel any pending requests for this service
					if (_pendingServices.TryGetValue(type, out var tcs))
					{
						tcs.TrySetCanceled();
						_pendingServices.Remove(type);
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

				foreach (var kvp in _services)
				{
					if (kvp.Value.Service is MonoBehaviour mb && mb == null)
					{
						servicesToRemove.Add(kvp.Key);
					}
				}

				foreach (var type in servicesToRemove)
				{
					_services.Remove(type);

					if (ServiceKitSettings.Instance.DebugLogging)
					{
						Debug.Log($"[ServiceKit] Cleaned up destroyed service: {type.Name}");
					}
				}
			}
		}

		/// <summary>
		/// Unregister a service
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
				if (!_services.Remove(serviceType, out var serviceInfo)) return;

				if (ServiceKitSettings.Instance.DebugLogging)
				{
					Debug.Log($"[ServiceKit] Unregistered {serviceType.Name}");
				}
			}
		}

		/// <summary>
		/// Get a service synchronously (returns null if not available)
		/// </summary>
		public T GetService<T>() where T : class
		{
			return GetService(typeof(T)) as T;
		}

		/// <summary>
		/// Get a service synchronously by type (returns null if not available)
		/// </summary>
		public object GetService(Type serviceType)
		{
			lock (_lock)
			{
				return _services.TryGetValue(serviceType, out var serviceInfo)
					? serviceInfo.Service
					: null;
			}
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
				if (_services.TryGetValue(serviceType, out var serviceInfo))
				{
					service = serviceInfo.Service;
					return service != null;
				}

				service = null;
				return false;
			}
		}

		/// <summary>
		/// Get a service asynchronously (waits until available)
		/// </summary>
		public async Task<T> GetServiceAsync<T>(CancellationToken cancellationToken = default) where T : class
		{
			object service = await GetServiceAsync(typeof(T), cancellationToken);
			return service as T;
		}

		/// <summary>
		/// Get a service asynchronously by type (waits until available)
		/// </summary>
		public async Task<object> GetServiceAsync(Type serviceType, CancellationToken cancellationToken = default)
		{
			TaskCompletionSource<object> tcs;

			lock (_lock)
			{
				if (_services.TryGetValue(serviceType, out var serviceInfo))
				{
					return serviceInfo.Service;
				}

				if (!_pendingServices.TryGetValue(serviceType, out tcs))
				{
					tcs = new TaskCompletionSource<object>();
					_pendingServices[serviceType] = tcs;
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
		/// Clear all registered services
		/// </summary>
		public void ClearServices()
		{
			lock (_lock)
			{
				_services.Clear();
				_trackedScenes.Clear();

				// Cancel all pending service requests
				foreach (var kvp in _pendingServices)
				{
					kvp.Value.TrySetCanceled();
				}
				_pendingServices.Clear();
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
	}
}