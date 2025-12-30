namespace ServiceKitSamples.SceneManagementExample
{
    /// <summary>
    /// Interface for global services that persist across scenes.
    /// </summary>
    public interface IGlobalService
    {
        string ServiceName { get; }
        int TotalSceneLoads { get; }
        void OnSceneLoaded(string sceneName);
    }
}
