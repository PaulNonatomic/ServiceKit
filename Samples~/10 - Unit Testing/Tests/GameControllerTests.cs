using System.Threading;
using NUnit.Framework;
using Nonatomic.ServiceKit;
using UnityEngine;

using System.Threading.Tasks;
#if SERVICEKIT_UNITASK
using Cysharp.Threading.Tasks;
#endif

// Test methods always return System.Threading.Tasks.Task so the NUnit runner can await them; the
// runner cannot await a UniTask-returning method. TestAwake() may return a UniTask under
// SERVICEKIT_UNITASK, but a UniTask is awaitable from inside an async Task, so the bodies are unchanged.

namespace ServiceKitSamples.UnitTestingExample.Tests
{
	/// <summary>
	/// Runnable EditMode tests for the Unit Testing sample. They use hand-written fakes (no external
	/// mocking library) and the <c>TestAwake()</c> pattern with <c>UseLocator()</c> to drive the full
	/// ServiceKitBehaviour lifecycle deterministically. This is the same approach the sample README
	/// describes with NSubstitute, kept dependency-free so it runs as-is.
	/// </summary>
	public class GameControllerTests
	{
		private sealed class FakeScoreService : IScoreService
		{
			public int CurrentScore { get; private set; }
			public int ResetCalls { get; private set; }

			public void AddScore(int points) => CurrentScore += points;
			public void Reset() { CurrentScore = 0; ResetCalls++; }
		}

		private sealed class FakeLeaderboardService : ILeaderboardService
		{
			public int SubmitCalls { get; private set; }
			public string LastPlayer { get; private set; }
			public int LastScore { get; private set; }

			public void SubmitScore(string playerName, int score)
			{
				SubmitCalls++;
				LastPlayer = playerName;
				LastScore = score;
			}

			public string GetTopPlayer() => LastPlayer;
		}

		private ServiceKitLocator _locator;
		private GameObject _gameObject;
		private GameController _controller;
		private FakeScoreService _score;
		private FakeLeaderboardService _leaderboard;

		[SetUp]
		public void SetUp()
		{
			_locator = ScriptableObject.CreateInstance<ServiceKitLocator>();

			// Inactive so Unity's Awake does not auto-run; TestAwake drives the lifecycle instead.
			_gameObject = new GameObject(nameof(GameController));
			_gameObject.SetActive(false);
			_controller = _gameObject.AddComponent<GameController>();
			_controller.UseLocator(_locator);

			_score = new FakeScoreService();
			_leaderboard = new FakeLeaderboardService();
			_locator.RegisterAndReadyService<IScoreService>(_score);
			_locator.RegisterAndReadyService<ILeaderboardService>(_leaderboard);
		}

		[TearDown]
		public void TearDown()
		{
			if (_gameObject != null) Object.DestroyImmediate(_gameObject);
			if (_locator != null)
			{
				_locator.ClearServices();
				Object.DestroyImmediate(_locator);
			}
		}

		[Test]
		public async Task TestAwake_InjectsBothDependencies()
		{
			await _controller.TestAwake(CancellationToken.None);

			Assert.AreSame(_score, _controller.ScoreService, "IScoreService should be injected");
			Assert.AreSame(_leaderboard, _controller.LeaderboardService, "ILeaderboardService should be injected");
		}

		[Test]
		public async Task CollectItem_AccumulatesScore()
		{
			await _controller.TestAwake(CancellationToken.None);

			_controller.CollectItem(50);
			_controller.CollectItem(25);

			Assert.AreEqual(75, _score.CurrentScore);
		}

		[Test]
		public async Task StartNewGame_ResetsScore()
		{
			await _controller.TestAwake(CancellationToken.None);
			_controller.CollectItem(50);

			_controller.StartNewGame("Ada");

			Assert.AreEqual(0, _score.CurrentScore);
			Assert.AreEqual(1, _score.ResetCalls);
		}

		[Test]
		public async Task EndGame_SubmitsCurrentScoreToLeaderboard()
		{
			await _controller.TestAwake(CancellationToken.None);
			_controller.CollectItem(120);

			_controller.EndGame("Ada");

			Assert.AreEqual(1, _leaderboard.SubmitCalls);
			Assert.AreEqual("Ada", _leaderboard.LastPlayer);
			Assert.AreEqual(120, _leaderboard.LastScore);
		}
	}
}
