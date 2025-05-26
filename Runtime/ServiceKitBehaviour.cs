using System.Threading.Tasks;
using UnityEngine;

namespace Nonatomic.ServiceKit
{
	public abstract class ServiceKitBehaviour<T> : MonoBehaviour where T : class 
	{
		[SerializeField] protected ServiceKitLocator _serviceKitLocator;

		protected async virtual void Awake()
		{
			await InjectServices();
		}

		protected virtual void RegisterService()
		{
			if (GuardAgainstUnassignedServiceKit()) return;
			
			_serviceKitLocator.RegisterService<T>(this as T);
		}

		protected async virtual Task InjectServices()
		{
			if (GuardAgainstUnassignedServiceKit()) return;
			
			await _serviceKitLocator.InjectServicesAsync(this)
				.WithCancellation(destroyCancellationToken) 
				.WithTimeout()
				.WithErrorHandling()
				.ExecuteAsync();
		}
		
		protected bool GuardAgainstUnassignedServiceKit()
		{
			if (_serviceKitLocator) return false;
			
			Debug.LogError($"{GetType().Name} requires a reference to a ServiceKitLocator.", this);
			return true;
		}
	}
}
