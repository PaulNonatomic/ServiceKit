using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Nonatomic.ServiceKit
{
	public interface IServiceKit
	{
		// --- Registration ---
		void RegisterService<T>(T serviceInstance) where T : class;

		// --- Retrieval ---
		bool HasService<T>() where T : class;
		T GetService<T>() where T : class;
		Task<T> GetServiceAsync<T>(CancellationToken cancellationToken = default) where T : class;

		// --- Injection ---
		/// <summary>
		///     Begins an asynchronous operation to inject services into the target object.
		///     Returns an operation object that can be configured with cancellation, timeout,
		///     and error handling, and then awaited.
		/// </summary>
		/// <param name="target">The object to inject services into.</param>
		/// <returns>An AsyncInjectionOperation to configure and await.</returns>
		AsyncInjectionOperation InjectServicesAsync(object target);
	}

	[CreateAssetMenu(fileName = "ServiceKit", menuName = "ServiceKit/ServiceKit Asset", order = 1)]
	// Updated class declaration to implement IServiceKit
	public class ServiceKit : ScriptableObject, IServiceKit
	{
		private readonly object _lock = new();
		private readonly Dictionary<Type, List<object>> _pendingRequests = new();
		private readonly Dictionary<Type, object> _registeredServices = new();

		public void RegisterService<T>(T serviceInstance) where T : class
		{
			if (serviceInstance == null)
			{
				Debug.LogError($"Cannot register a null service for type {typeof(T).FullName}");
				return;
			}

			var serviceType = typeof(T);
			lock (_lock)
			{
				if (_registeredServices.ContainsKey(serviceType))
				{
					Debug.LogWarning($"Overwriting existing service registration for type {serviceType.FullName}");
				}

				_registeredServices[serviceType] = serviceInstance;
				Debug.Log($"Service registered: {serviceType.FullName}");

				if (_pendingRequests.TryGetValue(serviceType, out var requests))
				{
					foreach (var tcsObject in requests.ToList())
					{
						var tcs = tcsObject as TaskCompletionSource<T>;
						if (tcs != null)
						{
							Task.Run(() => tcs.TrySetResult(serviceInstance));
						}
					}

					_pendingRequests.Remove(serviceType);
				}
			}
		}

		public bool HasService<T>() where T : class
		{
			lock (_lock)
			{
				return _registeredServices.ContainsKey(typeof(T));
			}
		}

		public T GetService<T>() where T : class
		{
			lock (_lock)
			{
				if (_registeredServices.TryGetValue(typeof(T), out var serviceInstance))
				{
					return (T)serviceInstance;
				}
			}

			Debug.LogWarning($"Service of type {typeof(T).FullName} not found.");
			return default;
		}

		public async Task<T> GetServiceAsync<T>(CancellationToken cancellationToken = default) where T : class
		{
			var serviceType = typeof(T);
			TaskCompletionSource<T> tcs;
			lock (_lock)
			{
				if (_registeredServices.TryGetValue(serviceType, out var serviceInstance))
				{
					return (T)serviceInstance;
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
					if (tcs.TrySetCanceled(cancellationToken))
					{
						lock (_lock)
						{
							if (_pendingRequests.TryGetValue(serviceType, out var requests))
							{
								requests.Remove(tcs);
								if (requests.Count == 0)
								{
									_pendingRequests.Remove(serviceType);
								}
							}
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
			// Pass 'this' (which implements IServiceKit) to the operation
			return new(this, target);
		}
	}
}