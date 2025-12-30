using System;
using System.Threading.Tasks;

namespace ServiceKitSamples.SceneManagementExample
{
    /// <summary>
    /// Service for managing scene transitions.
    /// </summary>
    public interface ISceneTransitionService
    {
        string CurrentScene { get; }
        bool IsTransitioning { get; }
        event Action<string> OnSceneLoadStarted;
        event Action<string> OnSceneLoadCompleted;
        Task LoadSceneAsync(string sceneName);
    }
}
