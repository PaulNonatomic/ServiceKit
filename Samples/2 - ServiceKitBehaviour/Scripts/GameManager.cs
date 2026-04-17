using System;
using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.ServiceKitBehaviourExample
{
	/// <summary>
	/// Game manager that demonstrates consuming multiple ServiceKitBehaviour-based services.
	///
	/// This shows how to:
	/// - Inject multiple services using [InjectService]
	/// - Use InitializeService() to set up interactions between services
	/// - Handle service events
	/// </summary>
	[Service(typeof(GameManager))] // Registers as concrete type (no interface needed)
	public class GameManager : ServiceKitBehaviour
	{
		[InjectService] private IPlayerService _playerService;
		[InjectService] private IScoreService _scoreService;

		protected override void InitializeService()
		{
			Debug.Log("[GameManager] All services injected, setting up game...");

			// Subscribe to score changes
			_scoreService.OnScoreChanged += OnScoreChanged;

			// Log initial state
			Debug.Log($"[GameManager] Player: {_playerService.PlayerName}");
			Debug.Log($"[GameManager] Health: {_playerService.Health}/{_playerService.MaxHealth}");
			Debug.Log($"[GameManager] Score: {_scoreService.CurrentScore}");
			Debug.Log($"[GameManager] High Score: {_scoreService.HighScore}");
		}

		protected override void HandleDependencyInjectionFailure(Exception exception)
		{
			Debug.LogError($"[GameManager] Failed to initialize: {exception.Message}");

			// In a real game, you might show an error screen or retry
			if (exception is TimeoutException)
			{
				Debug.LogError("[GameManager] Services took too long to become available.");
			}
		}

		private void OnScoreChanged(int newScore)
		{
			Debug.Log($"[GameManager] Score updated to: {newScore}");

			// Award health bonus every 100 points
			if (newScore > 0 && newScore % 100 == 0)
			{
				_playerService.Heal(10);
				Debug.Log("[GameManager] Bonus health awarded!");
			}
		}

		private void OnDestroy()
		{
			// Clean up event subscriptions
			if (_scoreService != null)
			{
				_scoreService.OnScoreChanged -= OnScoreChanged;
			}
		}

		// Public methods that could be called by UI or other game systems
		public void SimulateGameplay()
		{
			Debug.Log("[GameManager] Simulating gameplay...");

			// Simulate collecting items
			_scoreService.AddScore(25);
			_scoreService.AddScore(50);
			_scoreService.AddScore(25); // This will trigger the 100 point bonus

			// Simulate taking damage
			_playerService.TakeDamage(30);
		}
	}
}
