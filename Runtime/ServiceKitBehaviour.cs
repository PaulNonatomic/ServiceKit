using System;
using System.Threading.Tasks;
using UnityEngine;

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
			
			ServiceKitLocator.RegisterService<T>(this as T);
			Registered = true;
			
			if (ServiceKitSettings.Instance.DebugLogging)
			{
				Debug.Log($"[{GetType().Name}] Service registered (not ready yet)");
			}
		}

		protected virtual void MarkServiceReady()
		{
			if (GuardAgainstUnassignedServiceKit()) return;
			if (!Registered) return;
			
			ServiceKitLocator.ReadyService<T>();
			Ready = true;
			
			if (ServiceKitSettings.Instance.DebugLogging)
			{
				Debug.Log($"[{GetType().Name}] Service is now READY!");
			}
		}
	   
		protected virtual void UnregisterService()
		{
			if (GuardAgainstUnassignedServiceKit()) return;
			
			Registered = false;
			Ready = false;
			ServiceKitLocator.UnregisterService(typeof(T));
		}

		protected virtual async Task InjectServicesAsync()
		{
			if (GuardAgainstUnassignedServiceKit()) return;
			
			if (ServiceKitSettings.Instance.DebugLogging)
			{
				Debug.Log($"[{GetType().Name}] Waiting for dependencies...");
			}
			
			await ServiceKitLocator.InjectServicesAsync(this)
				.WithCancellation(destroyCancellationToken) 
				.WithTimeout()
				.WithErrorHandling(OnServiceInjectionFailed)
				.ExecuteAsync();
			
			if (ServiceKitSettings.Instance.DebugLogging)
			{
				Debug.Log($"[{GetType().Name}] Dependencies injected!");
			}
		}

		/// <summary>
		/// Override this to perform initialization after dependencies are injected
		/// but before the service becomes ready
		/// </summary>
		protected virtual async Task InitializeServiceAsync()
		{
			// Default implementation does nothing
			// Override in derived classes to perform initialization
			await Task.CompletedTask;
		}

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
	   
		protected bool GuardAgainstUnassignedServiceKit()
		{
			if (ServiceKitLocator) return false;
			
			Debug.LogError($"{GetType().Name} requires a reference to a ServiceKitLocator.", this);
			return true;
		}
	}
}