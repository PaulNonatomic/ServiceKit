using UnityEngine;

namespace ServiceKitSamples.ServiceTagsExample
{
	/// <summary>
	/// Settings service - tagged as "saveable" only (not resettable - keeps user preferences).
	/// </summary>
	public class SettingsService : ISaveable
	{
		public float MasterVolume { get; set; } = 1.0f;
		public float MusicVolume { get; set; } = 0.8f;
		public float SfxVolume { get; set; } = 1.0f;
		public bool Fullscreen { get; set; } = true;
		public int QualityLevel { get; set; } = 2;

		public string SaveKey => "settings";

		public object GetSaveData()
		{
			return new SettingsSaveData
			{
				MasterVolume = MasterVolume,
				MusicVolume = MusicVolume,
				SfxVolume = SfxVolume,
				Fullscreen = Fullscreen,
				QualityLevel = QualityLevel
			};
		}

		public void LoadSaveData(object data)
		{
			if (data is SettingsSaveData saveData)
			{
				MasterVolume = saveData.MasterVolume;
				MusicVolume = saveData.MusicVolume;
				SfxVolume = saveData.SfxVolume;
				Fullscreen = saveData.Fullscreen;
				QualityLevel = saveData.QualityLevel;
				Debug.Log($"[SettingsService] Loaded settings (Volume: {MasterVolume}, Quality: {QualityLevel})");
			}
		}

		[System.Serializable]
		public class SettingsSaveData
		{
			public float MasterVolume;
			public float MusicVolume;
			public float SfxVolume;
			public bool Fullscreen;
			public int QualityLevel;
		}
	}
}
