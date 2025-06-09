using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Nonatomic.ServiceKit
{
	public abstract class ServiceKitBehaviour<T> : MonoBehaviour where T : class 
	{
		[SerializeField] protected ServiceKitLocator ServiceKitLocator;

		protected async virtual void Awake()
		{
			await InjectServices();
		}

		protected virtual void RegisterService()
		{
			if (GuardAgainstUnassignedServiceKit()) return;
			
			ServiceKitLocator.RegisterService<T>(this as T);
		}

		protected async virtual Task InjectServices()
		{
			if (GuardAgainstUnassignedServiceKit()) return;
			
			await ServiceKitLocator.InjectServicesAsync(this)
				.WithCancellation(destroyCancellationToken) 
				.WithTimeout()
				.WithErrorHandling(OnServiceInjectionFailed)
				.ExecuteAsync();

			OnServicesInjected();
		}

		/// <summary>
		/// Called after all services have been successfully injected.
		/// Override this method to perform initialization that depends on injected services.
		/// </summary>
		protected virtual void OnServicesInjected()
		{
			// Default implementation does nothing
		}

		/// <summary>
		/// Called when service injection fails.
		/// Override this method to handle injection failures gracefully.
		/// </summary>
		/// <param name="exception">The exception that caused the injection to fail</param>
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
