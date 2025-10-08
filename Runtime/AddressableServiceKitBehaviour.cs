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
	[Serializable]
	public class ServiceKitLocatorAssetReference : AssetReferenceT<ServiceKitLocator>
	{
		public ServiceKitLocatorAssetReference(string guid) : base(guid) { }
	}

	public abstract class AddressableServiceKitBehaviour<T> : ServiceKitBehaviourBase<T> where T : class
	{
		[SerializeField] private ServiceKitLocatorAssetReference _serviceKitLocatorReference;

		private ServiceKitLocator _loadedServiceKitLocator;
		private AsyncOperationHandle<ServiceKitLocator> _loadHandle;
		private bool _isLoadHandleValid;

		protected override ServiceKitLocator ServiceKitLocator
		{
			get => _loadedServiceKitLocator;
			set => _loadedServiceKitLocator = value;
		}

		protected override async void Awake()
		{
			if (IsObjectDestroyed()) return;

			CacheDestroyToken();

			await LoadServiceKitLocatorAsync();

			if (IsObjectDestroyed()) return;

			RegisterServiceWithLocator();

			await PerformServiceInitializationSequence();
		}

		private bool IsObjectDestroyed()
		{
			return !this || !gameObject;
		}

#if SERVICEKIT_UNITASK
		private async UniTask PerformServiceInitializationSequence()
#else
		private async Task PerformServiceInitializationSequence()
#endif
		{
			await InjectDependenciesAsync();
			await InitializeServiceAsync();

			InitializeService();
			MarkServiceAsReady();
		}

#if SERVICEKIT_UNITASK
		private async UniTask LoadServiceKitLocatorAsync()
#else
		private async Task LoadServiceKitLocatorAsync()
#endif
		{
			if (!IsAssetReferenceValid())
			{
				LogInvalidAssetReferenceError();
				return;
			}

			_loadHandle = _serviceKitLocatorReference.LoadAssetAsync<ServiceKitLocator>();
			_isLoadHandleValid = true;

#if SERVICEKIT_UNITASK
			_loadedServiceKitLocator = await _loadHandle.ToUniTask();
#else
			_loadedServiceKitLocator = await _loadHandle.Task;
#endif

			if (_loadHandle.Status != AsyncOperationStatus.Succeeded)
			{
				LogAssetLoadFailureError();
			}
		}

		private bool IsAssetReferenceValid()
		{
			return _serviceKitLocatorReference != null && _serviceKitLocatorReference.RuntimeKeyIsValid();
		}

		private void LogInvalidAssetReferenceError()
		{
			Debug.LogError($"[ServiceKit] {GetType().Name} has an invalid or missing AssetReference to a ServiceKitLocator.", this);
		}

		private void LogAssetLoadFailureError()
		{
			Debug.LogError($"[ServiceKit] {GetType().Name} failed to load ServiceKitLocator from AssetReference.", this);
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
			ReleaseAddressableAsset();
		}

		private void ReleaseAddressableAsset()
		{
			if (!_isLoadHandleValid) return;

			Addressables.Release(_loadHandle);
			_isLoadHandleValid = false;
			_loadedServiceKitLocator = null;
		}
	}
}
#endif
