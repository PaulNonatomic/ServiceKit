using System.Collections.Generic;
using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.ServiceTagsExample
{
    /// <summary>
    /// Demonstrates service tag filtering for common game scenarios.
    ///
    /// Tags allow you to:
    /// - Find all saveable services for game save
    /// - Find all resettable services for new game
    /// - Organize services by category (core, ui, network)
    /// </summary>
    public class TagFilteringDemo : MonoBehaviour
    {
        [SerializeField] private ServiceKitLocator _serviceKitLocator;

        // Services created and registered with tags
        private PlayerDataService _playerData;
        private InventoryService _inventory;
        private SettingsService _settings;
        private AchievementService _achievements;
        private SessionService _session;

        private void Start()
        {
            Debug.Log("[TagFilteringDemo] Registering services with tags...\n");

            RegisterServices();
            DemonstrateTags();
        }

        private void RegisterServices()
        {
            // PlayerData: saveable + resettable
            _playerData = new PlayerDataService();
            _serviceKitLocator.Register(_playerData)
                .As<PlayerDataService>()
                .WithTags("saveable", "resettable", "gameplay")
                .Ready();

            // Inventory: saveable + resettable
            _inventory = new InventoryService();
            _serviceKitLocator.Register(_inventory)
                .As<InventoryService>()
                .WithTags("saveable", "resettable", "gameplay")
                .Ready();

            // Settings: saveable only (not reset on new game)
            _settings = new SettingsService();
            _serviceKitLocator.Register(_settings)
                .As<SettingsService>()
                .WithTags("saveable", "settings")
                .Ready();

            // Achievements: saveable only (permanent progress)
            _achievements = new AchievementService();
            _serviceKitLocator.Register(_achievements)
                .As<AchievementService>()
                .WithTags("saveable", "progression")
                .Ready();

            // Session: resettable only (not persisted)
            _session = new SessionService();
            _serviceKitLocator.Register(_session)
                .As<SessionService>()
                .WithTags("resettable", "runtime")
                .Ready();

            Debug.Log("[TagFilteringDemo] All services registered!\n");
        }

        private void DemonstrateTags()
        {
            // Set up some data
            _playerData.Level = 5;
            _playerData.Experience = 1500;
            _inventory.AddItem("sword", 1);
            _inventory.AddItem("potion", 3);
            _achievements.Unlock("first_blood");
            _session.EnemiesDefeated = 10;

            Debug.Log("=== Tag Filtering Demonstrations ===\n");

            // Demo 1: Find all saveable services
            DemoSaveGame();

            // Demo 2: Find all resettable services
            DemoNewGame();

            // Demo 3: Using GetServicesWithAnyTag
            DemoAnyTag();

            // Demo 4: Using GetServicesWithAllTags
            DemoAllTags();
        }

        private void DemoSaveGame()
        {
            Debug.Log("--- Demo: Save Game (all 'saveable' services) ---");

            var saveableServices = _serviceKitLocator.GetServicesWithTag("saveable");
            Debug.Log($"Found {saveableServices.Count} saveable services:");

            var saveData = new Dictionary<string, object>();
            foreach (var serviceInfo in saveableServices)
            {
                if (serviceInfo.Service is ISaveable saveable)
                {
                    saveData[saveable.SaveKey] = saveable.GetSaveData();
                    Debug.Log($"  - Saved: {saveable.SaveKey}");
                }
            }

            Debug.Log($"Total save data entries: {saveData.Count}\n");
        }

        private void DemoNewGame()
        {
            Debug.Log("--- Demo: New Game (all 'resettable' services) ---");

            var resettableServices = _serviceKitLocator.GetServicesWithTag("resettable");
            Debug.Log($"Found {resettableServices.Count} resettable services:");

            foreach (var serviceInfo in resettableServices)
            {
                if (serviceInfo.Service is IResettable resettable)
                {
                    Debug.Log($"  - Would reset: {serviceInfo.Service.GetType().Name}");
                    // resettable.Reset(); // Uncomment to actually reset
                }
            }

            Debug.Log("(Settings and Achievements are NOT reset - they're not tagged 'resettable')\n");
        }

        private void DemoAnyTag()
        {
            Debug.Log("--- Demo: GetServicesWithAnyTag('gameplay', 'settings') ---");

            var services = _serviceKitLocator.GetServicesWithAnyTag("gameplay", "settings");
            Debug.Log($"Found {services.Count} services with 'gameplay' OR 'settings' tag:");

            foreach (var serviceInfo in services)
            {
                Debug.Log($"  - {serviceInfo.Service.GetType().Name}");
            }
            Debug.Log("");
        }

        private void DemoAllTags()
        {
            Debug.Log("--- Demo: GetServicesWithAllTags('saveable', 'resettable') ---");

            var services = _serviceKitLocator.GetServicesWithAllTags("saveable", "resettable");
            Debug.Log($"Found {services.Count} services with BOTH 'saveable' AND 'resettable' tags:");

            foreach (var serviceInfo in services)
            {
                Debug.Log($"  - {serviceInfo.Service.GetType().Name}");
            }
            Debug.Log("(Only gameplay services that save and reset - NOT settings, achievements, or session)\n");
        }

        // Public methods that could be called from UI
        public void SaveGame()
        {
            Debug.Log("\n[SaveGame] Saving all saveable services...");
            var saveables = _serviceKitLocator.GetServicesWithTag("saveable");
            foreach (var info in saveables)
            {
                if (info.Service is ISaveable saveable)
                {
                    var data = saveable.GetSaveData();
                    // In real implementation: serialize and write to disk
                    Debug.Log($"  Saved {saveable.SaveKey}");
                }
            }
        }

        public void NewGame()
        {
            Debug.Log("\n[NewGame] Resetting all resettable services...");
            var resettables = _serviceKitLocator.GetServicesWithTag("resettable");
            foreach (var info in resettables)
            {
                if (info.Service is IResettable resettable)
                {
                    resettable.Reset();
                }
            }
        }
    }
}
