using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.CompleteGameExample
{
	/// <summary>
	/// Save/load service using tags to find saveable services.
	/// Demonstrates: Service tags for dynamic discovery.
	/// </summary>
	[Service(typeof(ISaveService))]
	public class SaveService : ServiceKitBehaviour, ISaveService
	{
		private static SaveService _instance;

		[InjectService] private IPlayerService _playerService;

		private const string SaveKey = "GameSave";

		public bool HasSaveData => PlayerPrefs.HasKey(SaveKey);

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
			Debug.Log($"[SaveService] Initialized. Has save: {HasSaveData}");
		}

		public void SaveGame()
		{
			Debug.Log("[SaveService] Saving game...");

			// In a real implementation, you'd serialize and save PlayerService data
			// using the tag-based approach from Sample 6
			var saveData = new GameSaveData
			{
				PlayerName = _playerService.PlayerName,
				Health = _playerService.Health,
				Score = _playerService.Score
			};

			var json = JsonUtility.ToJson(saveData);
			PlayerPrefs.SetString(SaveKey, json);
			PlayerPrefs.Save();

			Debug.Log($"[SaveService] Game saved! Score: {saveData.Score}");
		}

		public bool LoadGame()
		{
			if (!HasSaveData)
			{
				Debug.Log("[SaveService] No save data found");
				return false;
			}

			Debug.Log("[SaveService] Loading game...");

			var json = PlayerPrefs.GetString(SaveKey);
			var saveData = JsonUtility.FromJson<GameSaveData>(json);

			// Apply to player service
			if (_playerService is PlayerService playerService)
			{
				playerService.LoadSaveData(new PlayerService.PlayerSaveData
				{
					PlayerName = saveData.PlayerName,
					Health = saveData.Health,
					Score = saveData.Score
				});
			}

			Debug.Log($"[SaveService] Game loaded! Score: {saveData.Score}");
			return true;
		}

		public void DeleteSave()
		{
			PlayerPrefs.DeleteKey(SaveKey);
			PlayerPrefs.Save();
			Debug.Log("[SaveService] Save data deleted");
		}

		protected override void OnDestroy()
		{
			if (_instance == this)
			{
				_instance = null;
			}
			base.OnDestroy();
		}

		[System.Serializable]
		private class GameSaveData
		{
			public string PlayerName;
			public int Health;
			public int Score;
		}
	}
}
