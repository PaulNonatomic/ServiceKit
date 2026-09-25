#if SERVICEKIT_ADDRESSABLES
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

#if SERVICEKIT_UNITASK
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace Nonatomic.ServiceKit
{
	/// <summary>
	/// A typed Addressables reference to a <see cref="ServiceKitLocator"/>, so the inspector slot only
	/// accepts locator assets.
	/// </summary>
	[Serializable]
	public class ServiceKitLocatorAssetReference : AssetReferenceT<ServiceKitLocator>
	{
		public ServiceKitLocatorAssetReference(string guid) : base(guid) { }
	}

	/// <summary>
	/// A <see cref="ServiceKitBehaviour"/> whose locator is loaded through Addressables instead of a direct
	/// serialized reference. The locator is loaded asynchronously before the service registers (so it is
	/// not pulled into every bundle or scene that references it), and the Addressables handle is released on
	/// destroy. Only compiled when the Addressables package is installed (the <c>SERVICEKIT_ADDRESSABLES</c>
	/// define). Assign the locator via the AssetReference field below; leave the inherited serialized
	/// <c>ServiceKitLocator</c> field empty.
	/// </summary>
	public abstract class AddressableServiceKitBehaviour : ServiceKitBehaviour
	{
		[SerializeField] private ServiceKitLocatorAssetReference _serviceKitLocatorReference;

		private ServiceKitLocator _loadedLocator;
		private AsyncOperationHandle<ServiceKitLocator> _loadHandle;
		private bool _hasLoadHandle;

		// Return the addressable-loaded locator; the inherited serialized field is unused for this variant.
		protected override IServiceLocator ResolveLocator() => _loadedLocator;

#if SERVICEKIT_UNITASK
		protected override async UniTask PrepareLocatorAsync()
#else
		protected override async Task PrepareLocatorAsync()
#endif
		{
			if (_serviceKitLocatorReference == null || !_serviceKitLocatorReference.RuntimeKeyIsValid())
			{
				Debug.LogError($"[ServiceKit] {GetType().Name} has an invalid or missing Addressables reference to a ServiceKitLocator.", this);
				return;
			}

			_loadHandle = _serviceKitLocatorReference.LoadAssetAsync<ServiceKitLocator>();
			_hasLoadHandle = true;

			// AsyncOperationHandle.Task is awaitable from both the UniTask and the Task build, and avoids a
			// dependency on UniTask's optional Addressables integration.
			_loadedLocator = await _loadHandle.Task;

			if (_loadHandle.Status != AsyncOperationStatus.Succeeded || _loadedLocator == null)
			{
				Debug.LogError($"[ServiceKit] {GetType().Name} failed to load its ServiceKitLocator from Addressables.", this);
			}
		}

		protected override void OnDestroy()
		{
			// Unregister via the base first (it still sees the loaded locator), then release the handle.
			base.OnDestroy();

			if (!_hasLoadHandle) return;
			Addressables.Release(_loadHandle);
			_hasLoadHandle = false;
			_loadedLocator = null;
		}
	}
}
#endif
