using System;
using System.Linq;
using UnityEngine;

#if SERVICEKIT_UNITASK
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace Nonatomic.ServiceKit
{
	public abstract class ServiceKitBehaviour<T> : MonoBehaviour where T : class 
	{
		[SerializeField] protected ServiceKitLocator ServiceKitLocator;

		protected bool Registered;
		protected bool Ready;

		protected virtual async void Awake()
		{
			RegisterService();
			
			await InjectServicesAsync();
			await InitializeServiceAsync();
			InitializeService();
			
			MarkServiceReady();
		}
	   
		protected virtual void OnDestroy()
		{
			UnregisterService();
		}

		protected virtual void RegisterService()
		{
			if (GuardAgainstUnassignedServiceKit()) return;
			
			var serviceInstance = CastToServiceInterface();
			RegisterServiceInstance(serviceInstance);
			LogServiceRegistration();
		}

		private T CastToServiceInterface()
		{
			var serviceInstance = this as T;
			if (serviceInstance != null) return serviceInstance;

			ThrowServiceCastException();
			return null; // Never reached due to exception
		}

		private void ThrowServiceCastException()
		{
			var serviceType = typeof(T);
			var implementationType = GetType();
			var implementedInterfaces = string.Join(", ", implementationType.GetInterfaces().Select(i => i.Name));
			
			var errorMessage = $"Failed to register service for '{implementationType.Name}' as '{serviceType.Name}'. " +
							  $"This typically means '{implementationType.Name}' does not implement interface '{serviceType.Name}'. " +
							  $"Current class '{implementationType.Name}' implements: [{implementedInterfaces}]. " +
							  $"Please ensure '{implementationType.Name}' properly implements '{serviceType.Name}'.";
			
			Debug.LogError($"[ServiceKit] {errorMessage}", this);
			throw new InvalidOperationException(errorMessage);
		}

		private void RegisterServiceInstance(T serviceInstance)
		{
			ServiceKitLocator.RegisterService<T>(serviceInstance);
			Registered = true;
		}

		private void LogServiceRegistration()
		{
			if (!ServiceKitSettings.Instance.DebugLogging) return;
			
			Debug.Log($"[{GetType().Name}] Service registered (not ready yet)");
		}

		protected virtual void MarkServiceReady()
		{
			if (GuardAgainstUnassignedServiceKit()) return;
			if (!Registered) return;
			
			SetServiceAsReady();
			LogServiceReady();
		}

		private void SetServiceAsReady()
		{
			ServiceKitLocator.ReadyService<T>();
			Ready = true;
		}

		private void LogServiceReady()
		{
			if (!ServiceKitSettings.Instance.DebugLogging) return;
			
			Debug.Log($"[{GetType().Name}] Service is now READY!");
		}
	   
		protected virtual void UnregisterService()
		{
			if (GuardAgainstUnassignedServiceKit()) return;
			
			ClearServiceState();
			RemoveServiceFromLocator();
		}

		private void ClearServiceState()
		{
			Registered = false;
			Ready = false;
		}

		private void RemoveServiceFromLocator()
		{
			ServiceKitLocator.UnregisterService(typeof(T));
		}

	#if SERVICEKIT_UNITASK
	protected virtual async UniTask InjectServicesAsync()
#else
	protected virtual async Task InjectServicesAsync()
#endif
		{
			if (GuardAgainstUnassignedServiceKit()) return;
			
			LogDependencyWaiting();
			await ExecuteDependencyInjection();
			LogDependencyCompletion();
		}

		private void LogDependencyWaiting()
		{
			if (!ServiceKitSettings.Instance.DebugLogging) return;
			
			Debug.Log($"[{GetType().Name}] Waiting for dependencies...");
		}

#if SERVICEKIT_UNITASK
		private async UniTask ExecuteDependencyInjection()
#else
		private async Task ExecuteDependencyInjection()
#endif
		{
			await ServiceKitLocator.InjectServicesAsync(this)
				.WithCancellation(destroyCancellationToken) 
				.WithTimeout()
				.WithErrorHandling(OnServiceInjectionFailed)
				.ExecuteAsync();
		}

		private void LogDependencyCompletion()
		{
			if (!ServiceKitSettings.Instance.DebugLogging) return;
			
			Debug.Log($"[{GetType().Name}] Dependencies injected!");
		}

		/// <summary>
		/// Override this to perform initialization after dependencies are injected
		/// but before the service becomes ready
		/// </summary>
#if SERVICEKIT_UNITASK
		protected virtual async UniTask InitializeServiceAsync()
		{
			// Default implementation does nothing
			// Override in derived classes to perform initialization
			await UniTask.CompletedTask;
		}
#else
		protected virtual async Task InitializeServiceAsync()
		{
			// Default implementation does nothing
			// Override in derived classes to perform initialization
			await Task.CompletedTask;
		}
#endif

		/// <summary>
		/// Override this to perform initialization after dependencies are injected
		/// but before the service becomes ready
		/// </summary>
		protected virtual void InitializeService()
		{
			// Default implementation does nothing
			// Override in derived classes to perform initialization
		}

		/// <summary>
		/// Called when service injection fails
		/// </summary>
		protected virtual void OnServiceInjectionFailed(Exception exception)
		{
			Debug.LogError($"Failed to inject required services: {exception.Message}", this);
		}
	   
		private bool GuardAgainstUnassignedServiceKit()
		{
			if (ServiceKitLocator != null) return false;
			
			LogMissingServiceKitError();
			return true;
		}

		private void LogMissingServiceKitError()
		{
			Debug.LogError($"{GetType().Name} requires a reference to a ServiceKitLocator.", this);
		}
	}
}