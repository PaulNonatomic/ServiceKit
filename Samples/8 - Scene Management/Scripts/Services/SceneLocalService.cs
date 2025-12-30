using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.SceneManagementExample
{
    /// <summary>
    /// A scene-local service that is automatically cleaned up when the scene unloads.
    /// Perfect for scene-specific managers, level controllers, etc.
    /// </summary>
    [Service(typeof(ISceneService))]
    public class SceneLocalService : ServiceBehaviour, ISceneService
    {
        [InjectService] private IGlobalService _globalService;

        public string SceneName { get; private set; }
        public bool IsActive { get; private set; }

        protected override void InitializeService()
        {
            SceneName = gameObject.scene.name;
            IsActive = true;

            Debug.Log($"[SceneLocalService] Initialized for scene: {SceneName}");

            // Notify global service
            _globalService.OnSceneLoaded(SceneName);
        }

        public void Initialize(string sceneName)
        {
            SceneName = sceneName;
            Debug.Log($"[SceneLocalService] Manually initialized for: {sceneName}");
        }

        protected override void OnDestroy()
        {
            IsActive = false;
            Debug.Log($"[SceneLocalService] Destroyed (scene: {SceneName})");

            // ServiceBehaviour automatically unregisters on destroy
            base.OnDestroy();
        }
    }
}
