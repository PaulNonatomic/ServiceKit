using System.Collections.Generic;

namespace ServiceKitSamples.FluentRegistrationExample
{
    /// <summary>
    /// Analytics service interface for tracking user events.
    /// </summary>
    public interface IAnalyticsService
    {
        void TrackEvent(string eventName);
        void TrackEvent(string eventName, Dictionary<string, object> properties);
        int TotalEventsTracked { get; }
    }
}
