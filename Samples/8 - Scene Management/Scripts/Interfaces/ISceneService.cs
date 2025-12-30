namespace ServiceKitSamples.SceneManagementExample
{
    /// <summary>
    /// Interface for scene-local services that are destroyed with the scene.
    /// </summary>
    public interface ISceneService
    {
        string SceneName { get; }
        bool IsActive { get; }
        void Initialize(string sceneName);
    }
}
