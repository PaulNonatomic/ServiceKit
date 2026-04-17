using System;
using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.ServiceKitBehaviourExample
{
	/// <summary>
	/// Score tracking service demonstrating:
	/// - [Service] attribute registration
	/// - [InjectService] for dependency injection
	/// - InitializeService() for using injected dependencies
	/// </summary>
	[Service(typeof(IScoreService))]
	public class ScoreService : ServiceKitBehaviour, IScoreService
	{
		// Inject the player service to access player information
		[InjectService] private IPlayerService _playerService;

		private int _currentScore;
		private int _highScore;

		public int CurrentScore => _currentScore;
		public int HighScore => _highScore;

		public event Action<int> OnScoreChanged;

		protected override void InitializeService()
		{
			// At this point, _playerService is guaranteed to be injected
			// (assuming it's registered and ready before this service needs it)
			Debug.Log($"[ScoreService] Initialized for player: {_playerService.PlayerName}");

			// Load high score (in a real game, this would come from save data)
			_highScore = PlayerPrefs.GetInt("HighScore", 0);
			Debug.Log($"[ScoreService] High score loaded: {_highScore}");
		}

		public void AddScore(int points)
		{
			_currentScore += points;
			Debug.Log($"[ScoreService] Added {points} points. Current score: {_currentScore}");

			if (_currentScore > _highScore)
			{
				_highScore = _currentScore;
				PlayerPrefs.SetInt("HighScore", _highScore);
				Debug.Log($"[ScoreService] New high score: {_highScore}!");
			}

			OnScoreChanged?.Invoke(_currentScore);
		}

		public void ResetScore()
		{
			_currentScore = 0;
			Debug.Log("[ScoreService] Score reset to 0");
			OnScoreChanged?.Invoke(_currentScore);
		}
	}
}
