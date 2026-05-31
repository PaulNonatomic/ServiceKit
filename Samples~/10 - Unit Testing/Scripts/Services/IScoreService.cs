namespace ServiceKitSamples.UnitTestingExample
{
	public interface IScoreService
	{
		int CurrentScore { get; }
		void AddScore(int points);
		void Reset();
	}
}
