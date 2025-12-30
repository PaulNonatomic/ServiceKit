using UnityEngine;

namespace ServiceKitSamples.ServiceTagsExample
{
    /// <summary>
    /// Player data service - tagged as "saveable" and "resettable".
    /// </summary>
    public class PlayerDataService : ISaveable, IResettable
    {
        public string PlayerName { get; set; } = "Hero";
        public int Level { get; set; } = 1;
        public int Experience { get; set; } = 0;

        public string SaveKey => "player_data";

        public object GetSaveData()
        {
            return new PlayerSaveData
            {
                PlayerName = PlayerName,
                Level = Level,
                Experience = Experience
            };
        }

        public void LoadSaveData(object data)
        {
            if (data is PlayerSaveData saveData)
            {
                PlayerName = saveData.PlayerName;
                Level = saveData.Level;
                Experience = saveData.Experience;
                Debug.Log($"[PlayerDataService] Loaded: {PlayerName} (Level {Level})");
            }
        }

        public void Reset()
        {
            PlayerName = "Hero";
            Level = 1;
            Experience = 0;
            Debug.Log("[PlayerDataService] Reset to initial state");
        }

        [System.Serializable]
        public class PlayerSaveData
        {
            public string PlayerName;
            public int Level;
            public int Experience;
        }
    }
}
