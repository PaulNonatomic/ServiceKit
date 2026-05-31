using System.Collections.Generic;
using System.Linq;
using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.UnitTestingExample
{
	/// <summary>
	/// Leaderboard service that depends on IScoreService.
	///
	/// Demonstrates:
	/// - [InjectService] for dependency injection
	/// - Service-to-service dependencies
	/// - How services with dependencies can be unit tested
	/// </summary>
	[Service(typeof(ILeaderboardService))]
	public class LeaderboardService : ServiceKitBehaviour, ILeaderboardService
	{
		[InjectService] private IScoreService _scoreService;

		private readonly Dictionary<string, int> _leaderboard = new();

		protected override void InitializeService()
		{
			Debug.Log("[LeaderboardService] Initialized. Ready to accept score submissions.");
		}

		public void SubmitScore(string playerName, int score)
		{
			if (_leaderboard.TryGetValue(playerName, out var existingScore))
			{
				if (score > existingScore)
				{
					_leaderboard[playerName] = score;
					Debug.Log($"[LeaderboardService] New high score for {playerName}: {score}");
				}
			}
			else
			{
				_leaderboard[playerName] = score;
				Debug.Log($"[LeaderboardService] {playerName} added to leaderboard with score: {score}");
			}
		}

		public string GetTopPlayer()
		{
			if (_leaderboard.Count == 0)
			{
				return null;
			}

			var topEntry = _leaderboard.OrderByDescending(kvp => kvp.Value).First();
			return topEntry.Key;
		}
	}
}
