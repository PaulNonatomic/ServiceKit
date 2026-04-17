using System;
using UnityEngine;

namespace ServiceKitSamples.ServiceTagsExample
{
	/// <summary>
	/// Session service - tagged as "resettable" only (not saved, runtime only).
	/// </summary>
	public class SessionService : IResettable
	{
		public DateTime SessionStartTime { get; private set; }
		public int EnemiesDefeated { get; set; }
		public int DeathCount { get; set; }
		public float PlayTime => (float)(DateTime.Now - SessionStartTime).TotalSeconds;

		public SessionService()
		{
			Reset();
		}

		public void Reset()
		{
			SessionStartTime = DateTime.Now;
			EnemiesDefeated = 0;
			DeathCount = 0;
			Debug.Log("[SessionService] New session started");
		}

		public void LogSessionStats()
		{
			Debug.Log($"[SessionService] Play time: {PlayTime:F1}s, Enemies: {EnemiesDefeated}, Deaths: {DeathCount}");
		}
	}
}
