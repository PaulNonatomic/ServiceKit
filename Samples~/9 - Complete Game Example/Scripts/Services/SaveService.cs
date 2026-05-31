using System.Collections.Generic;
using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.CompleteGameExample
{
	/// <summary>
	/// Save/load service that uses tags to discover saveable services dynamically.
	/// Demonstrates: GetServicesWithTag for decoupled save/load without hardcoded dependencies.
	/// </summary>
	[Service(typeof(ISaveService))]
	public class SaveService : ServiceKitBehaviour, ISaveService
	{
		private static SaveService _instance;

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

			// Discover all saveable services dynamically via the "saveable" tag
			var saveableServices = Locator.GetServicesWithTag("saveable");
			var saveData = new Dictionary<string, string>();

			foreach (var serviceInfo in saveableServices)
			{
				if (serviceInfo.Service is ISaveable saveable)
				{
					var json = JsonUtility.ToJson(saveable.GetSaveData());
					saveData[saveable.SaveKey] = json;
					Debug.Log($"[SaveService] Saved: {saveable.SaveKey}");
				}
			}

			// Serialize the combined save data
			var wrapper = new SaveDataWrapper { Keys = new List<string>(), Values = new List<string>() };
			foreach (var kvp in saveData)
			{
				wrapper.Keys.Add(kvp.Key);
				wrapper.Values.Add(kvp.Value);
			}

			PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(wrapper));
			PlayerPrefs.Save();

			Debug.Log($"[SaveService] Game saved! ({saveData.Count} services)");
		}

		public bool LoadGame()
		{
			if (!HasSaveData)
			{
				Debug.Log("[SaveService] No save data found");
				return false;
			}

			Debug.Log("[SaveService] Loading game...");

			var wrapper = JsonUtility.FromJson<SaveDataWrapper>(PlayerPrefs.GetString(SaveKey));
			var saveData = new Dictionary<string, string>();
			for (int i = 0; i < wrapper.Keys.Count; i++)
			{
				saveData[wrapper.Keys[i]] = wrapper.Values[i];
			}

			// Discover all saveable services and restore their data
			var saveableServices = Locator.GetServicesWithTag("saveable");
			var loadedCount = 0;

			foreach (var serviceInfo in saveableServices)
			{
				if (serviceInfo.Service is ISaveable saveable && saveData.TryGetValue(saveable.SaveKey, out var json))
				{
					saveable.LoadSaveData(JsonUtility.FromJson(json, saveable.GetSaveData().GetType()));
					Debug.Log($"[SaveService] Loaded: {saveable.SaveKey}");
					loadedCount++;
				}
			}

			Debug.Log($"[SaveService] Game loaded! ({loadedCount} services)");
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
		private class SaveDataWrapper
		{
			public List<string> Keys;
			public List<string> Values;
		}
	}
}
