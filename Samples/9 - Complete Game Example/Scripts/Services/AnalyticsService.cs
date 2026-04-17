using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.CompleteGameExample
{
	/// <summary>
	/// Optional analytics service.
	/// Demonstrates: Optional dependency pattern (can be removed without breaking the game).
	/// </summary>
	[Service(typeof(IAnalyticsService))]
	public class AnalyticsService : ServiceKitBehaviour, IAnalyticsService
	{
		private static AnalyticsService _instance;

		[SerializeField] private bool _enabled = true;

		protected override void Awake()
		{
			if (_instance != null && _instance != this)
			{
				Destroy(gameObject);
				return;
			}
			_instance = this;
			DontDestroyOnLoad(gameObject);
			base.Awake();
		}

		protected override void InitializeService()
		{
			Debug.Log($"[AnalyticsService] Initialized (Enabled: {_enabled})");
		}

		public void TrackEvent(string eventName)
		{
			if (!_enabled) return;
			Debug.Log($"[Analytics] Event: {eventName}");
		}

		public void TrackGameStart()
		{
			if (!_enabled) return;
			Debug.Log("[Analytics] Game Started");
		}

		public void TrackGameEnd(int score, float playTime)
		{
			if (!_enabled) return;
			Debug.Log($"[Analytics] Game Ended - Score: {score}, PlayTime: {playTime:F1}s");
		}

		public void TrackAchievement(string achievementId)
		{
			if (!_enabled) return;
			Debug.Log($"[Analytics] Achievement: {achievementId}");
		}

		protected override void OnDestroy()
		{
			if (_instance == this)
			{
				_instance = null;
			}
			base.OnDestroy();
		}
	}
}
