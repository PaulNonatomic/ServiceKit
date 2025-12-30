using System;
using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.OptionalDependencyExample
{
    /// <summary>
    /// Optional ad service.
    /// Add this to the scene to enable ads, or leave it out.
    /// </summary>
    [Service(typeof(IAdService))]
    public class AdService : ServiceBehaviour, IAdService
    {
        [SerializeField] private bool _adsEnabled = true;

        public bool AdsEnabled => _adsEnabled;

        protected override void InitializeService()
        {
            Debug.Log($"[AdService] Initialized (Ads Enabled: {_adsEnabled})");
        }

        public void ShowInterstitial()
        {
            if (!_adsEnabled)
            {
                Debug.Log("[AdService] Interstitial skipped - ads disabled");
                return;
            }
            Debug.Log("[AdService] Showing interstitial ad...");
        }

        public void ShowRewarded(Action<bool> onComplete)
        {
            if (!_adsEnabled)
            {
                Debug.Log("[AdService] Rewarded ad skipped - ads disabled, granting reward anyway");
                onComplete?.Invoke(true);
                return;
            }
            Debug.Log("[AdService] Showing rewarded ad...");
            // Simulate ad completion
            onComplete?.Invoke(true);
        }
    }
}
