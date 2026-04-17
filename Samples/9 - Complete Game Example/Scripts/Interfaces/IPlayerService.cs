using System;

namespace ServiceKitSamples.CompleteGameExample
{
	/// <summary>
	/// Manages player state and actions.
	/// </summary>
	public interface IPlayerService
	{
		string PlayerName { get; }
		int Health { get; }
		int MaxHealth { get; }
		int Score { get; }
		bool IsAlive { get; }

		event Action<int> OnHealthChanged;
		event Action<int> OnScoreChanged;
		event Action OnPlayerDied;

		void Initialize(string playerName);
		void TakeDamage(int amount);
		void Heal(int amount);
		void AddScore(int points);
		void Reset();
	}
}
