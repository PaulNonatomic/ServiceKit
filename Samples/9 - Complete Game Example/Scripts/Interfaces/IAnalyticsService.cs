namespace ServiceKitSamples.CompleteGameExample
{
	/// <summary>
	/// Optional analytics service.
	/// </summary>
	public interface IAnalyticsService
	{
		void TrackEvent(string eventName);
		void TrackGameStart();
		void TrackGameEnd(int score, float playTime);
		void TrackAchievement(string achievementId);
	}
}
