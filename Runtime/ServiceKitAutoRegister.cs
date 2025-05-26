using UnityEngine;
using UnityEngine.Serialization;

namespace Nonatomic.ServiceKit
{
	/// <summary>
	/// Automatically registers a MonoBehaviour as a service when it's created
	/// </summary>
	public abstract class ServiceKitAutoRegister<T> : MonoBehaviour where T : class
	{
		[FormerlySerializedAs("_serviceKitLocatorLocator")]
        [FormerlySerializedAs("_serviceLocator")]
		[FormerlySerializedAs("_serviceKit")]
		[SerializeField] protected ServiceKitLocator _serviceKitLocator;
		[SerializeField] private bool _registerOnAwake = true;
		[SerializeField] private bool _unregisterOnDestroy = true;

		protected virtual void Awake()
		{
			if (_registerOnAwake && _serviceKitLocator != null)
			{
				RegisterService();
			}
		}

		protected virtual void OnDestroy()
		{
			if (_unregisterOnDestroy && _serviceKitLocator != null)
			{
				UnregisterService();
			}
		}

		protected virtual void RegisterService()
		{
			if (this is T service)
			{
				_serviceKitLocator.RegisterService<T>(service, GetType().Name);
			}
			else
			{
				Debug.LogError($"[ServiceKitAutoRegister] {GetType().Name} does not implement {typeof(T).Name}");
			}
		}

		protected virtual void UnregisterService()
		{
			_serviceKitLocator.UnregisterService<T>();
		}
	}
}
