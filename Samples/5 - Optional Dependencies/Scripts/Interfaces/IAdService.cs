namespace ServiceKitSamples.OptionalDependencyExample
{
    /// <summary>
    /// Advertising service that may or may not be available.
    /// Example: Disabled for premium users, enabled for free users.
    /// </summary>
    public interface IAdService
    {
        void ShowInterstitial();
        void ShowRewarded(System.Action<bool> onComplete);
        bool AdsEnabled { get; }
    }
}
