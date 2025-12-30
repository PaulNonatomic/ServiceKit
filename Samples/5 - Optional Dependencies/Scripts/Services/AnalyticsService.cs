using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.OptionalDependencyExample
{
    /// <summary>
    /// Optional analytics service.
    /// Add this to the scene to enable analytics, or leave it out.
    /// </summary>
    [Service(typeof(IAnalyticsService))]
    public class AnalyticsService : ServiceBehaviour, IAnalyticsService
    {
        [SerializeField] private bool _enabled = true;

        public bool IsEnabled => _enabled;

        protected override void InitializeService()
        {
            Debug.Log($"[AnalyticsService] Initialized (Enabled: {_enabled})");
        }

        public void TrackEvent(string eventName)
        {
            if (!_enabled) return;
            Debug.Log($"[Analytics] Event: {eventName}");
        }

        public void TrackScreenView(string screenName)
        {
            if (!_enabled) return;
            Debug.Log($"[Analytics] Screen View: {screenName}");
        }
    }
}
