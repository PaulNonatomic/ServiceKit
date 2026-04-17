using System.Collections.Generic;
using System.Threading.Tasks;
using Nonatomic.ServiceKit;
using UnityEngine;

#if SERVICEKIT_UNITASK
using Cysharp.Threading.Tasks;
#endif

namespace ServiceKitSamples.AsyncResolutionExample
{
	/// <summary>
	/// Cloud save service with slow initialization and async operations.
	/// Depends on both NetworkService and AuthService.
	/// </summary>
	[Service(typeof(ICloudSaveService))]
	public class CloudSaveService : ServiceKitBehaviour, ICloudSaveService
	{
		[InjectService] private INetworkService _networkService;
		[InjectService] private IAuthService _authService;

		[SerializeField] private float _initializationDelay = 0.5f;

		private Dictionary<string, string> _localCache = new();

		public bool IsReady { get; private set; }

#if SERVICEKIT_UNITASK
		protected override async UniTask InitializeServiceAsync()
#else
		protected override async Task InitializeServiceAsync()
#endif
		{
			Debug.Log("[CloudSaveService] Initializing cloud save system...");

			// Simulate loading cloud metadata
			await Task.Delay((int)(_initializationDelay * 1000));

			IsReady = true;
			Debug.Log($"[CloudSaveService] Ready (Network: {_networkService.IsConnected}, Auth: {_authService.IsAuthenticated})");
		}

		public async Task<bool> SaveAsync(string key, string data)
		{
			if (!IsReady)
			{
				Debug.LogWarning("[CloudSaveService] Not ready!");
				return false;
			}

			Debug.Log($"[CloudSaveService] Saving: {key}");
			_localCache[key] = data;

			// Simulate cloud sync
			await Task.Delay(100);

			Debug.Log($"[CloudSaveService] Saved: {key}");
			return true;
		}

		public async Task<string> LoadAsync(string key)
		{
			if (!IsReady)
			{
				Debug.LogWarning("[CloudSaveService] Not ready!");
				return null;
			}

			Debug.Log($"[CloudSaveService] Loading: {key}");

			// Simulate cloud fetch
			await Task.Delay(100);

			if (_localCache.TryGetValue(key, out var data))
			{
				Debug.Log($"[CloudSaveService] Loaded: {key}");
				return data;
			}

			Debug.Log($"[CloudSaveService] Key not found: {key}");
			return null;
		}
	}
}
