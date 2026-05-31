namespace ServiceKitSamples.CompleteGameExample
{
	/// <summary>
	/// Interface for services that can save and load their state.
	/// Services implementing this should be registered with the "saveable" tag
	/// so SaveService can discover them dynamically.
	/// </summary>
	public interface ISaveable
	{
		string SaveKey { get; }
		object GetSaveData();
		void LoadSaveData(object data);
	}
}
