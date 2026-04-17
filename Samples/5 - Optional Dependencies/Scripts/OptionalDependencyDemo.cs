using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.OptionalDependencyExample
{
	/// <summary>
	/// Demonstrates optional dependency injection using the [InjectService(Required = false)] attribute.
	///
	/// The 3-state dependency resolution works as follows:
	/// 1. Service is ready → inject immediately
	/// 2. Service is registered but not ready → wait for it
	/// 3. Service is not registered → skip (field stays null)
	/// </summary>
	[Service(typeof(OptionalDependencyDemo))]
	public class OptionalDependencyDemo : ServiceKitBehaviour
	{
		// Required dependency - will fail if not available
		[InjectService]
		private ICoreService _coreService;

		// Optional dependency - will be null if analytics service is not in the scene
		[InjectService(Required = false)]
		private IAnalyticsService _analyticsService;

		// Optional dependency - will be null if ad service is not in the scene
		[InjectService(Required = false)]
		private IAdService _adService;

		protected override void InitializeService()
		{
			Debug.Log("[OptionalDependencyDemo] Initialized with dependencies:");
			Debug.Log($"  - ICoreService: {(_coreService != null ? "INJECTED" : "NULL")} (required)");
			Debug.Log($"  - IAnalyticsService: {(_analyticsService != null ? "INJECTED" : "NULL")} (optional)");
			Debug.Log($"  - IAdService: {(_adService != null ? "INJECTED" : "NULL")} (optional)");

			// Demonstrate graceful degradation
			DemonstrateGracefulDegradation();
		}

		private void DemonstrateGracefulDegradation()
		{
			Debug.Log("\n--- Demonstrating Graceful Degradation ---\n");

			// Core service - always available
			Debug.Log($"Game Version: {_coreService.GameVersion}");

			// Analytics - works if available, safely skipped if not
			TrackGameStart();

			// Ads - works if available, provides alternative if not
			TryShowAd();
		}

		private void TrackGameStart()
		{
			// Safe pattern for optional services
			if (_analyticsService != null)
			{
				_analyticsService.TrackEvent("game_start");
				_analyticsService.TrackScreenView("MainMenu");
				Debug.Log("[Demo] Analytics tracked game start");
			}
			else
			{
				Debug.Log("[Demo] Analytics not available - skipping tracking");
			}
		}

		private void TryShowAd()
		{
			if (_adService != null && _adService.AdsEnabled)
			{
				_adService.ShowRewarded(success =>
				{
					Debug.Log($"[Demo] Rewarded ad completed: {success}");
				});
			}
			else
			{
				Debug.Log("[Demo] Ad service not available - granting reward directly");
				// Grant reward without showing ad (premium user experience)
			}
		}

		/// <summary>
		/// Example: Level complete handler with optional services
		/// </summary>
		public void OnLevelComplete(int score)
		{
			Debug.Log($"\n[Demo] Level complete with score: {score}");

			// Track with analytics if available
			_analyticsService?.TrackEvent($"level_complete_score_{score}");

			// Show interstitial if ads are available
			_adService?.ShowInterstitial();
		}
	}
}
