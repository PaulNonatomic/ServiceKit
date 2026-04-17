namespace ServiceKitSamples.OptionalDependencyExample
{
	/// <summary>
	/// Analytics service that may or may not be available.
	/// Example: Disabled in development builds, enabled in production.
	/// </summary>
	public interface IAnalyticsService
	{
		void TrackEvent(string eventName);
		void TrackScreenView(string screenName);
		bool IsEnabled { get; }
	}
}
