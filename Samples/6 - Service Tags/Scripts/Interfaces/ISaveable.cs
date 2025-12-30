namespace ServiceKitSamples.ServiceTagsExample
{
    /// <summary>
    /// Interface for services that can save their state.
    /// </summary>
    public interface ISaveable
    {
        string SaveKey { get; }
        object GetSaveData();
        void LoadSaveData(object data);
    }
}
