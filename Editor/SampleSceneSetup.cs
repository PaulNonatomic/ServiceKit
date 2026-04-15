using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nonatomic.ServiceKit.Editor
{
    /// <summary>
    /// Editor utility for setting up ServiceKit sample scenes.
    /// After importing a sample, use the menu to configure the scene with proper components.
    /// </summary>
    public static class SampleSceneSetup
    {
        private const string MenuPath = "Tools/ServiceKit/Setup Sample Scene";

        // Explicit name mappings for GameObjects that don't match their script names exactly
        private static readonly Dictionary<string, string> NameMappings = new Dictionary<string, string>
        {
            // Sample 4 - Multi-Type Registration
            { "AudioConsumerDemo", "AudioConsumerDemo" },
            { "MenuMusicController", "MenuMusicController" },
            { "WeaponSfxController", "WeaponSfxController" },

            // Sample 5 - Optional Dependencies (handles parenthetical notes)
            { "AnalyticsService (Optional)", "AnalyticsService" },
            { "AdService (Optional)", "AdService" },

            // Sample 7 - Async Resolution
            { "AsyncConsumerDemo", "AsyncConsumerDemo" },
            { "TimeoutDemo", "TimeoutDemo" },

            // Sample 8 - Scene Management
            { "BootstrapManager", "BootstrapManager" },
            { "GlobalPersistentService", "GlobalPersistentService" },
            { "SceneTransitionService", "SceneTransitionService" },
            { "MenuSceneController", "MenuSceneController" },
            { "GameplaySceneController", "GameplaySceneController" },
            { "SceneLocalService", "SceneLocalService" },

            // Sample 9 - Complete Game Example
            { "GameBootstrap", "GameBootstrap" },
            { "GameDemo", "GameDemo" },
            { "GameStateService", "GameStateService" },
            { "PlayerService", "PlayerService" },
            { "SaveService", "SaveService" },
            { "UIService", "UIService" },
            { "AudioManager", "AudioManager" },
            { "AnalyticsService", "AnalyticsService" },
        };

        [MenuItem(MenuPath)]
        public static void SetupCurrentScene()
        {
            var scene = SceneManager.GetActiveScene();
            var sceneName = scene.name;

            Debug.Log($"[ServiceKit] Setting up sample scene: {sceneName}");

            // Find or create ServiceKitLocator
            var locator = FindOrCreateServiceKitLocator();
            if (locator == null)
            {
                Debug.LogError("[ServiceKit] Could not find or create ServiceKitLocator asset. Please create one manually.");
                return;
            }

            // Get all root GameObjects
            var rootObjects = scene.GetRootGameObjects();

            // Setup components based on GameObject names
            int componentsAdded = 0;
            foreach (var go in rootObjects)
            {
                componentsAdded += SetupGameObject(go, locator);
            }

            if (componentsAdded > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log($"[ServiceKit] Scene setup complete! Added {componentsAdded} component(s).");
                Debug.Log("[ServiceKit] Remember to save the scene (Ctrl+S).");
            }
            else
            {
                Debug.Log("[ServiceKit] No components needed to be added. Scene may already be set up.");
            }
        }

        private static ServiceKitLocator FindOrCreateServiceKitLocator()
        {
            // Try to find existing locator in project
            var guids = AssetDatabase.FindAssets("t:ServiceKitLocator");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<ServiceKitLocator>(path);
            }

            // Create new locator
            var locator = ScriptableObject.CreateInstance<ServiceKitLocator>();
            var assetPath = "Assets/ServiceKitLocator.asset";

            AssetDatabase.CreateAsset(locator, assetPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[ServiceKit] Created new ServiceKitLocator at: {assetPath}");
            return locator;
        }

        private static int SetupGameObject(GameObject go, ServiceKitLocator locator)
        {
            int added = 0;

            // Skip standard Unity objects
            if (go.name == "Main Camera" || go.name == "Directional Light")
                return 0;

            // Try to find and add component based on name
            var componentType = FindComponentType(go.name);
            if (componentType != null && go.GetComponent(componentType) == null)
            {
                var component = go.AddComponent(componentType);
                added++;

                // If it's a ServiceBehaviour or has ServiceKitLocator field, try to assign locator
                TryAssignLocator(component, locator);

                Debug.Log($"[ServiceKit] Added {componentType.Name} to {go.name}");
            }

            return added;
        }

        private static Type FindComponentType(string gameObjectName)
        {
            // First check explicit name mappings
            string typeName;
            if (NameMappings.TryGetValue(gameObjectName, out typeName))
            {
                // Use the mapped name
            }
            else
            {
                // Clean up the name (remove parenthetical notes)
                typeName = gameObjectName.Split('(')[0].Trim();
            }

            // Search for the type in all assemblies
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                try
                {
                    var types = assembly.GetTypes();
                    foreach (var type in types)
                    {
                        if (type.Name == typeName && typeof(MonoBehaviour).IsAssignableFrom(type))
                        {
                            return type;
                        }
                    }
                }
                catch
                {
                    // Ignore assemblies that can't be reflected
                }
            }

            return null;
        }

        private static void TryAssignLocator(Component component, ServiceKitLocator locator)
        {
            if (component == null || locator == null) return;

            var type = component.GetType();

            // ServiceBehaviour uses a public ServiceKitLocator field
            var field = type.GetField("ServiceKitLocator",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance);

            if (field != null && field.FieldType == typeof(ServiceKitLocator))
            {
                field.SetValue(component, locator);
            }
        }

        [MenuItem(MenuPath, true)]
        public static bool ValidateSetupCurrentScene()
        {
            // Only enable if a scene is loaded
            return SceneManager.GetActiveScene().isLoaded;
        }
    }
}
