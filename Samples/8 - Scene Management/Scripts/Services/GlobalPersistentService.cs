using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.SceneManagementExample
{
    /// <summary>
    /// A global service that persists across scene loads.
    /// Uses DontDestroyOnLoad to survive scene transitions.
    /// </summary>
    [Service(typeof(IGlobalService))]
    public class GlobalPersistentService : ServiceBehaviour, IGlobalService
    {
        private static GlobalPersistentService _instance;

        public string ServiceName => "GlobalPersistentService";
        public int TotalSceneLoads { get; private set; }

        protected override void Awake()
        {
            // Singleton pattern for global service
            if (_instance != null && _instance != this)
            {
                Debug.Log("[GlobalPersistentService] Duplicate instance destroyed");
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            // Call base.Awake() to register with ServiceKit
            base.Awake();
        }

        protected override void InitializeService()
        {
            Debug.Log("[GlobalPersistentService] Initialized - will persist across scenes");
        }

        public void OnSceneLoaded(string sceneName)
        {
            TotalSceneLoads++;
            Debug.Log($"[GlobalPersistentService] Scene loaded: {sceneName} (Total loads: {TotalSceneLoads})");
        }

        protected override void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
            base.OnDestroy();
        }
    }
}
