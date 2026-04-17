using System.Collections.Generic;
using UnityEngine;

namespace ServiceKitSamples.ServiceTagsExample
{
	/// <summary>
	/// Achievement service - tagged as "saveable" only (achievements are permanent).
	/// </summary>
	public class AchievementService : ISaveable
	{
		private HashSet<string> _unlockedAchievements = new();

		public string SaveKey => "achievements";

		public bool IsUnlocked(string achievementId)
		{
			return _unlockedAchievements.Contains(achievementId);
		}

		public void Unlock(string achievementId)
		{
			if (_unlockedAchievements.Add(achievementId))
			{
				Debug.Log($"[AchievementService] Achievement unlocked: {achievementId}");
			}
		}

		public int UnlockedCount => _unlockedAchievements.Count;

		public object GetSaveData()
		{
			return new List<string>(_unlockedAchievements);
		}

		public void LoadSaveData(object data)
		{
			if (data is List<string> achievements)
			{
				_unlockedAchievements = new HashSet<string>(achievements);
				Debug.Log($"[AchievementService] Loaded {_unlockedAchievements.Count} achievements");
			}
		}
	}
}
