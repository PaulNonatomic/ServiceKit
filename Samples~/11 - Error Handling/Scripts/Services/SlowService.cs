using System;
using Nonatomic.ServiceKit;
using UnityEngine;

#if SERVICEKIT_UNITASK
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace ServiceKitSamples.ErrorHandlingExample
{
	/// <summary>
	/// A service that simulates slow initialization (e.g., loading remote config, connecting to a server).
	///
	/// Demonstrates:
	/// - Async initialization with a 2-second delay
	/// - Dependency on IConfigService via [InjectService]
	/// - How slow services interact with timeout handling
	/// </summary>
	[Service(typeof(ISlowService))]
	public class SlowService : ServiceKitBehaviour, ISlowService
	{
		[InjectService] private IConfigService _configService;

		private bool _isReady;

		public bool IsReady => _isReady;

#if SERVICEKIT_UNITASK
		protected override async UniTask InitializeServiceAsync()
#else
		protected override async Task InitializeServiceAsync()
#endif
		{
			Debug.Log("[SlowService] Starting slow initialization...");

			// Simulate a slow initialization (e.g., network call, asset loading)
#if SERVICEKIT_UNITASK
			await UniTask.Delay(TimeSpan.FromSeconds(2), cancellationToken: CachedDestroyToken);
#else
			await Task.Delay(TimeSpan.FromSeconds(2), CachedDestroyToken);
#endif

			_isReady = true;
			Debug.Log($"[SlowService] Initialization complete. Config: {_configService.AppName}");
		}

		public string GetData()
		{
			return _isReady ? "SlowService data is available" : "SlowService is still initializing";
		}

		protected override void HandleDependencyInjectionFailure(Exception exception)
		{
			Debug.LogError($"[SlowService] Dependency injection failed: {exception.Message}");
		}
	}
}
