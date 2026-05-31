using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.UnitTestingExample
{
	/// <summary>
	/// A simple score tracking service.
	///
	/// Demonstrates:
	/// - [Service] attribute for interface-based registration
	/// - ServiceKitBehaviour lifecycle
	/// - Testable via UseLocator() + TestAwake()
	/// </summary>
	[Service(typeof(IScoreService))]
	public class ScoreService : ServiceKitBehaviour, IScoreService
	{
		private int _currentScore;

		public int CurrentScore => _currentScore;

		protected override void InitializeService()
		{
			_currentScore = 0;
			Debug.Log("[ScoreService] Initialized with score: 0");
		}

		public void AddScore(int points)
		{
			_currentScore += points;
			Debug.Log($"[ScoreService] Added {points} points. Current score: {_currentScore}");
		}

		public void Reset()
		{
			_currentScore = 0;
			Debug.Log("[ScoreService] Score reset to 0");
		}
	}
}
