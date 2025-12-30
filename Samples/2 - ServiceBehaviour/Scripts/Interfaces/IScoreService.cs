using System;

namespace ServiceKitSamples.ServiceBehaviourExample
{
    /// <summary>
    /// Service interface for score tracking.
    /// </summary>
    public interface IScoreService
    {
        int CurrentScore { get; }
        int HighScore { get; }

        event Action<int> OnScoreChanged;

        void AddScore(int points);
        void ResetScore();
    }
}
