namespace ServiceKitSamples.UnitTestingExample
{
	public interface ILeaderboardService
	{
		void SubmitScore(string playerName, int score);
		string GetTopPlayer();
	}
}
