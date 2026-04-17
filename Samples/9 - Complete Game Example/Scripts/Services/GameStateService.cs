using System;
using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.CompleteGameExample
{
	/// <summary>
	/// Central game state manager.
	/// Demonstrates: ServiceKitBehaviour, event-based architecture, service coordination.
	/// </summary>
	[Service(typeof(IGameStateService))]
	public class GameStateService : ServiceKitBehaviour, IGameStateService
	{
		[InjectService] private IPlayerService _playerService;
		[InjectService] private IUIService _uiService;
		[InjectService] private IMusicService _musicService;

		[InjectService(Required = false)]
		private IAnalyticsService _analyticsService;

		private float _gameStartTime;

		public GameState CurrentState { get; private set; } = GameState.MainMenu;
		public event Action<GameState, GameState> OnStateChanged;

		protected override void Awake()
		{
			DontDestroyOnLoad(gameObject);
			base.Awake();
		}

		protected override void InitializeService()
		{
			Debug.Log("[GameStateService] Initialized");

			// Subscribe to player death
			_playerService.OnPlayerDied += HandlePlayerDeath;

			// Show main menu
			SetState(GameState.MainMenu);
		}

		public void SetState(GameState newState)
		{
			if (CurrentState == newState) return;

			var oldState = CurrentState;
			CurrentState = newState;

			Debug.Log($"[GameStateService] State: {oldState} -> {newState}");

			HandleStateChange(newState);
			OnStateChanged?.Invoke(oldState, newState);
		}

		private void HandleStateChange(GameState newState)
		{
			switch (newState)
			{
				case GameState.MainMenu:
					_uiService.ShowMainMenu();
					_musicService.PlayMusic("MenuTheme");
					Time.timeScale = 1f;
					break;

				case GameState.Playing:
					_uiService.ShowGameplay();
					_musicService.PlayMusic("GameplayTheme");
					Time.timeScale = 1f;
					break;

				case GameState.Paused:
					_uiService.ShowPauseMenu();
					Time.timeScale = 0f;
					break;

				case GameState.GameOver:
					_uiService.ShowGameOver(_playerService.Score);
					_musicService.PlayMusic("GameOverTheme");
					Time.timeScale = 1f;

					var playTime = Time.realtimeSinceStartup - _gameStartTime;
					_analyticsService?.TrackGameEnd(_playerService.Score, playTime);
					break;
			}
		}

		public void StartGame()
		{
			Debug.Log("[GameStateService] Starting game...");

			_playerService.Reset();
			_playerService.Initialize("Player");
			_gameStartTime = Time.realtimeSinceStartup;

			_analyticsService?.TrackGameStart();

			SetState(GameState.Playing);
		}

		public void PauseGame()
		{
			if (CurrentState == GameState.Playing)
			{
				SetState(GameState.Paused);
			}
		}

		public void ResumeGame()
		{
			if (CurrentState == GameState.Paused)
			{
				SetState(GameState.Playing);
			}
		}

		public void EndGame()
		{
			SetState(GameState.GameOver);
		}

		private void HandlePlayerDeath()
		{
			Debug.Log("[GameStateService] Player died!");
			EndGame();
		}

		protected override void OnDestroy()
		{
			if (_playerService != null)
			{
				_playerService.OnPlayerDied -= HandlePlayerDeath;
			}

			base.OnDestroy();
		}
	}
}
