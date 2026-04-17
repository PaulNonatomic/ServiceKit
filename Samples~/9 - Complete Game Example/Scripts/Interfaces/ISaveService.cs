namespace ServiceKitSamples.CompleteGameExample
{
	/// <summary>
	/// Handles saving and loading game data.
	/// </summary>
	public interface ISaveService
	{
		bool HasSaveData { get; }
		void SaveGame();
		bool LoadGame();
		void DeleteSave();
	}
}
