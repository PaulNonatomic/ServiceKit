using System;
using System.Threading;
using Nonatomic.ServiceKit;
using UnityEngine;

#if SERVICEKIT_UNITASK
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace ServiceKitSamples.UnitTestingExample
{
	/// <summary>
	/// Game controller that depends on both IScoreService and ILeaderboardService.
	///
	/// Demonstrates:
	/// - Multiple [InjectService] dependencies
	/// - The TestAwake() pattern for unit testing
	/// - How to replicate the full ServiceKitBehaviour lifecycle in tests
	/// </summary>
	[Service(typeof(GameController))]
	public class GameController : ServiceKitBehaviour
	{
		[InjectService] private IScoreService _scoreService;
		[InjectService] private ILeaderboardService _leaderboardService;

		public IScoreService ScoreService => _scoreService;
		public ILeaderboardService LeaderboardService => _leaderboardService;

		protected override void InitializeService()
		{
			Debug.Log("[GameController] All dependencies injected. Game ready.");
		}

		protected override void HandleDependencyInjectionFailure(Exception exception)
		{
			Debug.LogError($"[GameController] Failed to initialize: {exception.Message}");
		}

		public void StartNewGame(string playerName)
		{
			_scoreService.Reset();
			Debug.Log($"[GameController] New game started for {playerName}");
		}

		public void CollectItem(int points)
		{
			_scoreService.AddScore(points);
		}

		public void EndGame(string playerName)
		{
			_leaderboardService.SubmitScore(playerName, _scoreService.CurrentScore);
			Debug.Log($"[GameController] Game over. Final score: {_scoreService.CurrentScore}");
		}

		/// <summary>
		/// Replicates the full ServiceKitBehaviour Awake lifecycle for unit testing.
		///
		/// Unity's Awake() is called automatically by AddComponent and cannot be invoked
		/// directly. This method allows tests to trigger the same registration, injection,
		/// initialization, and ready sequence using UseLocator().
		/// </summary>
#if SERVICEKIT_UNITASK
		public async UniTask TestAwake(CancellationToken cancellationToken)
#else
		public async Task TestAwake(CancellationToken cancellationToken)
#endif
		{
			RegisterServiceWithLocator();

			await Locator.Inject(this)
				.WithCancellation(cancellationToken)
				.WithTimeout()
				.WithErrorHandling(HandleDependencyInjectionFailure)
				.ExecuteAsync();

			await InitializeServiceAsync();
			InitializeService();
			MarkServiceAsReady();
		}
	}
}
