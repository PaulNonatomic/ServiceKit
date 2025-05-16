using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nonatomic.ServiceKit
{
	public interface IServiceKit
	{
		// --- Registration ---
		void RegisterService<T>(T serviceInstance) where T : class;

		// --- Unregistration ---
		bool UnregisterService<T>() where T : class;

		// --- Retrieval ---
		bool HasService<T>() where T : class;
		T GetService<T>() where T : class;
		Task<T> GetServiceAsync<T>(CancellationToken cancellationToken = default) where T : class;
		Scene GetSceneForService<T>() where T : class;

		// --- Injection ---
		AsyncInjectionOperation InjectServicesAsync(object target);

		// --- Configuration ---
		TimeSpan GetDefaultAsyncInjectionTimeout();
	}

	[CreateAssetMenu(fileName = "ServiceKit", menuName = "ServiceKit/ServiceKit Asset", order = 1)]
	public class ServiceKit : ScriptableObject, IServiceKit
	{
		public event Action OnChange;
		
		[Tooltip("Default timeout in seconds for async injection operations if not explicitly set on the operation via WithTimeout(TimeSpan) or WithTimeout(double).")] [SerializeField]
		private float _defaultAsyncInjectionTimeoutInSeconds = 30.0f;

		private readonly object _lock = new();
		private readonly Dictionary<Type, List<object>> _pendingRequests = new();

		// Updated dictionary to store ServiceRecord
		private readonly Dictionary<Type, ServiceRecord> _registeredServiceRecords = new();

		public TimeSpan GetDefaultAsyncInjectionTimeout()
		{
			return TimeSpan.FromSeconds(_defaultAsyncInjectionTimeoutInSeconds);
		}

		public void RegisterService<T>(T serviceInstance) where T : class
		{
			if (serviceInstance == null)
			{
				Debug.LogError($"Cannot register a null service for type {typeof(T).FullName}");
				return;
			}

			var serviceType = typeof(T);
			var serviceRecord = new ServiceRecord(serviceInstance);

			lock (_lock)
			{
				if (_registeredServiceRecords.ContainsKey(serviceType))
				{
					Debug.LogWarning($"Overwriting existing service registration for type {serviceType.FullName}");
				}

				_registeredServiceRecords[serviceType] = serviceRecord;

				var sceneInfo = serviceRecord.OwningScene.IsValid() 
					? $" in scene '{serviceRecord.OwningScene.name}'" 
					: "";
				
				Debug.Log($"Service registered: {serviceType.FullName}{sceneInfo}");

				if (!_pendingRequests.TryGetValue(serviceType, out var requests)) return;
				
				foreach (var tcsObject in requests.ToList())
				{
					var tcs = tcsObject as TaskCompletionSource<T>;
					
					// Complete with the actual instance, not the record, to keep TaskCompletionSource<T>
					if (tcs != null)
					{
						Task.Run(() => tcs.TrySetResult((T)serviceRecord.Instance));
					}
				}

				_pendingRequests.Remove(serviceType);
			}
			
			OnChange?.Invoke();
		}

		public bool UnregisterService<T>() where T : class
		{
			var serviceType = typeof(T);
			var removed = false;

			lock (_lock)
			{
				if (_registeredServiceRecords.Remove(serviceType))
				{
					removed = true;
					Debug.Log($"Service unregistered: {serviceType.FullName}");

					// Cancel any pending GetServiceAsync requests for this service type
					if (!_pendingRequests.TryGetValue(serviceType, out var pendingList)) return removed;
				
					// Create a copy for safe iteration as TrySetCanceled can trigger continuations
					var tcsToCancel = pendingList.ToList();
					_pendingRequests.Remove(serviceType); // Remove the entry for this type

					var cancelledCount = 0;
					foreach (var tcsObject in tcsToCancel)
					{
						if (tcsObject is not TaskCompletionSource<T> tcs) continue;
						
						// Schedule the cancellation to run on the thread pool
						// This avoids holding the lock for too long and allows continuations to run freely.
						Task.Run(() =>
						{
							if (tcs.TrySetCanceled())
							{
								// Incrementing a shared counter here would require interlocked operations.
								// For simplicity, we log outside or just a general message.
							}
						});
						cancelledCount++;
					}

					if (cancelledCount > 0)
					{
						Debug.Log($"Cancelled {cancelledCount} pending async request(s) for unregistered service: {serviceType.FullName}");
					}
				}
				else
				{
					Debug.LogWarning($"Attempted to unregister service of type {serviceType.FullName}, but it was not found registered.");
				}
			}

			OnChange?.Invoke();
			return removed;
		}

		public bool HasService<T>() where T : class
		{
			lock (_lock)
			{
				return _registeredServiceRecords.ContainsKey(typeof(T));
			}
		}

		public T GetService<T>() where T : class
		{
			var serviceType = typeof(T);
			lock (_lock)
			{
				if (_registeredServiceRecords.TryGetValue(serviceType, out var record))
				{
					return (T)record.Instance;
				}
			}

			Debug.LogWarning($"Service of type {serviceType.FullName} not found.");
			return default;
		}

		public async Task<T> GetServiceAsync<T>(CancellationToken cancellationToken = default) where T : class
		{
			var serviceType = typeof(T);
			TaskCompletionSource<T> tcs;
			lock (_lock)
			{
				if (_registeredServiceRecords.TryGetValue(serviceType, out var record))
				{
					return (T)record.Instance;
				}

				tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
				if (!_pendingRequests.TryGetValue(serviceType, out var requests))
				{
					requests = new();
					_pendingRequests[serviceType] = requests;
				}

				requests.Add(tcs);
			}

			CancellationTokenRegistration cancellationRegistration = default;
			if (cancellationToken.CanBeCanceled)
			{
				cancellationRegistration = cancellationToken.Register(() =>
				{
					if (!tcs.TrySetCanceled(cancellationToken)) return;
					
					lock (_lock)
					{
						if (!_pendingRequests.TryGetValue(serviceType, out var requests)) return;
					
						requests.Remove(tcs);
						if (requests.Count == 0)
						{
							_pendingRequests.Remove(serviceType);
						}
					}
				});
			}

			try
			{
				return await tcs.Task;
			}
			finally
			{
				if (cancellationToken.CanBeCanceled)
				{
					await cancellationRegistration.DisposeAsync();
				}
			}
		}


		public AsyncInjectionOperation InjectServicesAsync(object target)
		{
			return new(this, target);
		}

		/// <summary>
		///     Gets the scene in which the specified service was registered, if it's a MonoBehaviour.
		///     Returns an invalid Scene struct if the service is not found, not a MonoBehaviour,
		///     or was not associated with a valid scene upon registration.
		/// </summary>
		public Scene GetSceneForService<T>() where T : class
		{
			var serviceType = typeof(T);
			lock (_lock)
			{
				if (_registeredServiceRecords.TryGetValue(serviceType, out var record))
				{
					return record.OwningScene;
				}
			}

			Debug.LogWarning($"Service of type {serviceType.FullName} not found when querying for scene.");
			return default;
		}

		#if UNITY_EDITOR
		/// <summary>
		///     FOR EDITOR USE ONLY.
		///     Returns a read-only snapshot of the currently registered service records.
		/// </summary>
		public IReadOnlyDictionary<Type, ServiceRecord> GetRegisteredServiceRecordsForEditor()
		{
			lock (_lock)
			{
				// Return a new dictionary to prevent modification issues if the editor iterates
				// while the collection changes, and to ensure it's a snapshot.
				return new ReadOnlyDictionary<Type, ServiceRecord>(new Dictionary<Type, ServiceRecord>(_registeredServiceRecords));
			}
		}

		/// <summary>
		///     FOR EDITOR USE ONLY.
		///     Returns a read-only list of pending service request types and their counts.
		/// </summary>
		public IReadOnlyList<KeyValuePair<Type, int>> GetPendingRequestsForEditor()
		{
			var pendingList = new List<KeyValuePair<Type, int>>();
			lock (_lock)
			{
				foreach (var kvp in _pendingRequests)
				{
					pendingList.Add(new(kvp.Key, kvp.Value.Count));
				}
			}

			return pendingList.AsReadOnly();
		}
		#endif
	}
}