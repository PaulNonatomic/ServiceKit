using System;
using System.Collections.Generic;
using System.Linq;
using Nonatomic.ServiceKit.Editor.Utils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nonatomic.ServiceKit.Editor
{
	/// <summary>
	/// Automatically assigns default ServiceKitLocator references without requiring inspector viewing
	/// </summary>
	public class ServiceKitLocatorAutoAssigner : AssetPostprocessor
	{
		/// <summary>
		/// Process all assets in the project to auto-assign ServiceKitLocator references
		/// </summary>
		[MenuItem("Tools/ServiceKit/Auto-Assign ServiceKit Locators")]
		public static void AutoAssignAllServiceKitLocators()
		{
			var processedCount = 0;
			var defaultLocator = GetDefaultServiceKitLocator();
			
			if (defaultLocator == null)
			{
				Debug.LogWarning("[ServiceKit] No ServiceKitLocator found in project. Auto-assignment skipped.");
				return;
			}

			// Find all prefabs and scene files
			var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
			var sceneGuids = AssetDatabase.FindAssets("t:Scene");
			var allGuids = prefabGuids.Concat(sceneGuids).ToArray();

			EditorUtility.DisplayProgressBar("ServiceKit Auto-Assignment", "Processing assets...", 0f);

			try
			{
				for (int i = 0; i < allGuids.Length; i++)
				{
					var guid = allGuids[i];
					var assetPath = AssetDatabase.GUIDToAssetPath(guid);
					
					EditorUtility.DisplayProgressBar("ServiceKit Auto-Assignment", 
						$"Processing {System.IO.Path.GetFileName(assetPath)}...", 
						(float)i / allGuids.Length);

					if (ProcessAsset(assetPath, defaultLocator))
					{
						processedCount++;
					}
				}
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}

			AssetDatabase.SaveAssets();
			Debug.Log($"[ServiceKit] Auto-assigned ServiceKitLocator to {processedCount} assets.");
		}

		/// <summary>
		/// Called after assets are imported - auto-assign ServiceKitLocators in new/modified assets
		/// </summary>
		static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
		{
			var defaultLocator = GetDefaultServiceKitLocator();
			if (defaultLocator == null) return;

			var processedAny = false;

			// Process imported and moved assets
			foreach (var assetPath in importedAssets.Concat(movedAssets))
			{
				if (assetPath.EndsWith(".prefab") || assetPath.EndsWith(".unity"))
				{
					if (ProcessAsset(assetPath, defaultLocator))
					{
						processedAny = true;
					}
				}
			}

			if (processedAny)
			{
				AssetDatabase.SaveAssets();
			}
		}

		/// <summary>
		/// Process a single asset (prefab or scene) for ServiceKitLocator auto-assignment
		/// </summary>
		private static bool ProcessAsset(string assetPath, ServiceKitLocator defaultLocator)
		{
			var modified = false;

			if (assetPath.EndsWith(".prefab"))
			{
				modified = ProcessPrefab(assetPath, defaultLocator);
			}
			else if (assetPath.EndsWith(".unity"))
			{
				modified = ProcessScene(assetPath, defaultLocator);
			}

			return modified;
		}

		/// <summary>
		/// Process a prefab for ServiceKitLocator references
		/// </summary>
		private static bool ProcessPrefab(string prefabPath, ServiceKitLocator defaultLocator)
		{
			var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
			if (prefab == null) return false;

			var modified = false;
			var components = prefab.GetComponentsInChildren<MonoBehaviour>(true);

			foreach (var component in components)
			{
				if (component == null) continue;

				var serializedObject = new SerializedObject(component);
				if (ProcessSerializedObject(serializedObject, defaultLocator))
				{
					serializedObject.ApplyModifiedProperties();
					modified = true;
				}
			}

			if (modified)
			{
				EditorUtility.SetDirty(prefab);
			}

			return modified;
		}

		/// <summary>
		/// Process a scene for ServiceKitLocator references
		/// </summary>
		private static bool ProcessScene(string scenePath, ServiceKitLocator defaultLocator)
		{
			var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
			var sceneToProcess = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
			
			if (!sceneToProcess.IsValid()) return false;

			var modified = false;
			var rootObjects = sceneToProcess.GetRootGameObjects();

			foreach (var rootObject in rootObjects)
			{
				var components = rootObject.GetComponentsInChildren<MonoBehaviour>(true);

				foreach (var component in components)
				{
					if (component == null) continue;

					var serializedObject = new SerializedObject(component);
					if (ProcessSerializedObject(serializedObject, defaultLocator))
					{
						serializedObject.ApplyModifiedProperties();
						modified = true;
					}
				}
			}

			if (modified)
			{
				EditorSceneManager.MarkSceneDirty(sceneToProcess);
				EditorSceneManager.SaveScene(sceneToProcess);
			}

			// Close the scene if it wasn't the original active scene
			if (sceneToProcess != currentScene)
			{
				EditorSceneManager.CloseScene(sceneToProcess, true);
			}

			return modified;
		}

		/// <summary>
		/// Process a SerializedObject for ServiceKitLocator field assignment
		/// </summary>
		private static bool ProcessSerializedObject(SerializedObject serializedObject, ServiceKitLocator defaultLocator)
		{
			var modified = false;
			var property = serializedObject.GetIterator();
			
			// Iterate through all properties
			if (property.NextVisible(true))
			{
				do
				{
					if (property.propertyType == SerializedPropertyType.ObjectReference &&
						property.objectReferenceValue == null)
					{
						// Check if this field expects a ServiceKitLocator
						var fieldType = GetPropertyFieldType(property);
						if (fieldType == typeof(ServiceKitLocator))
						{
							property.objectReferenceValue = defaultLocator;
							modified = true;
						}
					}
				}
				while (property.NextVisible(false));
			}

			return modified;
		}

		/// <summary>
		/// Get the field type of a SerializedProperty using reflection
		/// </summary>
		private static System.Type GetPropertyFieldType(SerializedProperty property)
		{
			var targetObject = property.serializedObject.targetObject;
			var targetType = targetObject.GetType();
			
			// Navigate to the field through the property path
			var fieldInfo = targetType.GetField(property.name, 
				System.Reflection.BindingFlags.Instance | 
				System.Reflection.BindingFlags.Public | 
				System.Reflection.BindingFlags.NonPublic);
			
			return fieldInfo?.FieldType;
		}

		/// <summary>
		/// Get the default ServiceKitLocator in the project with smart selection logic
		/// </summary>
		private static ServiceKitLocator GetDefaultServiceKitLocator()
		{
			// PRIORITY 0A: Check ServiceKitSettings ScriptableObject first (highest priority - visible in inspector)
			var settingsDefault = ServiceKitSettings.Instance.DefaultServiceKitLocator;
			if (settingsDefault != null)
			{
				return settingsDefault;
			}

			// PRIORITY 0B: Check project settings (EditorPrefs) as fallback
			var configuredDefault = ServiceKitProjectSettings.DefaultServiceKitLocator;
			if (configuredDefault != null)
			{
				return configuredDefault;
			}

			var allLocators = AssetUtils.FindAssetsByType<ServiceKitLocator>();
			if (allLocators == null || allLocators.Count == 0) return null;

			// If only one locator, use it
			if (allLocators.Count == 1) return allLocators[0];

			// Multiple locators - use smart selection logic
			// Priority 1: Look for the package-included ServiceKit (most predictable default)
			var packageServiceKit = GetPackageIncludedServiceKit(allLocators);
			if (packageServiceKit != null)
			{
				return packageServiceKit;
			}

			// Priority 2: Look for one named "Default", "Main", or "ServiceKit" (case insensitive)
			var priorityNames = new[] { "default", "main", "servicekit", "global" };
			foreach (var priorityName in priorityNames)
			{
				var preferredLocator = allLocators.FirstOrDefault(l => 
					l.name.ToLowerInvariant().Contains(priorityName));
				if (preferredLocator != null)
				{
					return preferredLocator;
				}
			}

			// Priority 3: Use the one in the Assets root folder
			var rootLocator = allLocators.FirstOrDefault(l =>
			{
				var path = AssetDatabase.GetAssetPath(l);
				return path.StartsWith("Assets/") && !path.Substring(7).Contains('/');
			});
			if (rootLocator != null)
			{
				return rootLocator;
			}

			// Priority 4: Use alphabetically first by name for consistency
			var sortedLocators = allLocators.OrderBy(l => l.name).ToList();
			Debug.LogWarning($"[ServiceKit] Multiple ServiceKitLocators found ({allLocators.Count}). Using '{sortedLocators[0].name}' as default. " +
				$"Set explicit default in ServiceKitSettings for full control.");
			
			return sortedLocators[0];
		}

		/// <summary>
		/// Get the package-included ServiceKit locator (most predictable default)
		/// </summary>
		private static ServiceKitLocator GetPackageIncludedServiceKit(List<ServiceKitLocator> allLocators)
		{
			// Look for ServiceKitLocator that's in the package directory
			// The package is typically at Packages/com.nonatomic.servicekit/ or in Assets if imported as asset
			foreach (var locator in allLocators)
			{
				var path = AssetDatabase.GetAssetPath(locator);
				
				// Check if it's in the ServiceKit package directory
				var isInPackage = path.Contains("com.nonatomic.servicekit") || 
								  path.Contains("ServiceKit/Runtime") ||
								  path.Contains("ServiceKit\\Runtime");
				
				// Also check if it's named "ServiceKit" (the canonical package-included one)
				var isCanonicalName = locator.name.Equals("ServiceKit", StringComparison.OrdinalIgnoreCase);
				
				if (isInPackage && isCanonicalName)
				{
					return locator;
				}
			}
			
			// Fallback: just look for one named "ServiceKit" regardless of location
			return allLocators.FirstOrDefault(l => l.name.Equals("ServiceKit", StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>
		/// Open or create ServiceKitSettings asset
		/// </summary>
		[MenuItem("Tools/ServiceKit/Open ServiceKit Settings")]
		public static void OpenServiceKitSettings()
		{
			var settings = ServiceKitSettings.Instance;
			if (settings.name.Contains("Runtime"))
			{
				// Runtime instance - need to create an asset
				if (EditorUtility.DisplayDialog("Create ServiceKitSettings Asset", 
					"No ServiceKitSettings asset found. Create one now?\n\n" +
					"This will make your settings persistent and visible in the inspector.", 
					"Create", "Cancel"))
				{
					var newSettings = ScriptableObjectUtils.CreateInstanceInProject<ServiceKitSettings>(
						fileName: "ServiceKitSettings",
						selectInstance: true
					);
					
					if (newSettings != null)
					{
						// Copy settings from runtime instance
						var runtimeSettings = settings;
						EditorUtility.CopySerialized(runtimeSettings, newSettings);
						EditorUtility.SetDirty(newSettings);
						AssetDatabase.SaveAssets();
						
						// Refresh the singleton to use the new asset
						ServiceKitSettings.RefreshInstance();
						
						Debug.Log($"[ServiceKit] Created ServiceKitSettings asset: {AssetDatabase.GetAssetPath(newSettings)}");
					}
				}
			}
			else
			{
				// Existing asset - just select it
				EditorGUIUtility.PingObject(settings);
				Selection.activeObject = settings;
			}
		}

		/// <summary>
		/// Create a default ServiceKitLocator if none exists
		/// </summary>
		[MenuItem("Tools/ServiceKit/Create Default ServiceKit Locator")]
		public static void CreateDefaultServiceKitLocator()
		{
			var existing = GetDefaultServiceKitLocator();
			if (existing != null)
			{
				Debug.Log($"[ServiceKit] Default ServiceKitLocator already exists: {existing.name}");
				EditorGUIUtility.PingObject(existing);
				return;
			}

			var newLocator = ScriptableObjectUtils.CreateInstanceInProject<ServiceKitLocator>(
				fileName: "Default ServiceKit",
				selectInstance: true
			);

			if (newLocator != null)
			{
				// Automatically set this as the project default if no explicit default is set
				if (!ServiceKitProjectSettings.HasDefaultServiceKitLocator)
				{
					ServiceKitProjectSettings.DefaultServiceKitLocator = newLocator;
					Debug.Log($"[ServiceKit] Created and set '{newLocator.name}' as default ServiceKitLocator in Project Settings");
				}
				else
				{
					Debug.Log($"[ServiceKit] Created default ServiceKitLocator: {newLocator.name}");
				}
			}
		}

		/// <summary>
		/// Validate the current project settings configuration
		/// </summary>
		[MenuItem("Tools/ServiceKit/Validate Configuration")]
		public static void ValidateConfiguration()
		{
			var allLocators = AssetUtils.FindAssetsByType<ServiceKitLocator>();
			var settingsDefault = ServiceKitSettings.Instance.DefaultServiceKitLocator;
			var configuredDefault = ServiceKitProjectSettings.DefaultServiceKitLocator;
			
			Debug.Log("=== ServiceKit Configuration Report ===");
			
			if (allLocators == null || allLocators.Count == 0)
			{
				Debug.LogWarning("[ServiceKit] No ServiceKitLocators found in project!");
				return;
			}
			
			Debug.Log($"[ServiceKit] Found {allLocators.Count} ServiceKitLocator(s):");
			foreach (var locator in allLocators.OrderBy(l => l.name))
			{
				var path = AssetDatabase.GetAssetPath(locator);
				Debug.Log($"  • {locator.name} at {path}");
			}
			
			Debug.Log("\n=== Default ServiceKitLocator Priority ===");
			
			if (settingsDefault != null)
			{
				Debug.Log($"[ServiceKit] ✓ ServiceKitSettings Asset Default: {settingsDefault.name} (HIGHEST PRIORITY)");
			}
			else
			{
				Debug.Log("[ServiceKit] ○ No default set in ServiceKitSettings asset");
			}
			
			if (configuredDefault != null)
			{
				Debug.Log($"[ServiceKit] ✓ Project Settings Default: {configuredDefault.name} (FALLBACK)");
			}
			else
			{
				Debug.Log("[ServiceKit] ○ No default set in Project Settings");
			}
			
			var packageServiceKit = GetPackageIncludedServiceKit(allLocators);
			if (packageServiceKit != null)
			{
				Debug.Log($"[ServiceKit] ✓ Package-Included ServiceKit: {packageServiceKit.name} (CONSISTENT FALLBACK)");
			}
			else
			{
				Debug.Log("[ServiceKit] ○ No package-included ServiceKit found");
			}
			
			var finalDefault = GetDefaultServiceKitLocator();
			if (finalDefault != null)
			{
				Debug.Log($"[ServiceKit] 🎯 ACTIVE DEFAULT: {finalDefault.name}");
			}
			else
			{
				Debug.LogWarning("[ServiceKit] ⚠ No default ServiceKitLocator could be determined!");
			}
			
			Debug.Log("\n=== Recommendations ===");
			if (settingsDefault == null && configuredDefault == null)
			{
				if (packageServiceKit != null)
				{
					Debug.Log("[ServiceKit] Using package-included ServiceKit as default. For custom control, set explicit default in ServiceKitSettings asset.");
				}
				else
				{
					Debug.Log("[ServiceKit] Consider setting an explicit default in ServiceKitSettings asset (most visible) or Project Settings → ServiceKit");
				}
			}
		}

		/// <summary>
		/// Debug method to compare ServiceKitLocator discovery methods
		/// </summary>
		[MenuItem("Tools/ServiceKit/Debug ServiceKitLocator Discovery")]
		public static void DebugServiceKitLocatorDiscovery()
		{
			Debug.Log("=== ServiceKitLocator Discovery Debug ===");
			
			// Method 1: AssetUtils
			var assetUtilsResults = AssetUtils.FindAssetsByType<ServiceKitLocator>();
			Debug.Log($"\n[AssetUtils.FindAssetsByType] Found {assetUtilsResults?.Count ?? 0} ServiceKitLocator(s):");
			if (assetUtilsResults != null)
			{
				foreach (var locator in assetUtilsResults)
				{
					var path = AssetDatabase.GetAssetPath(locator);
					Debug.Log($"  • {locator.name} at {path}");
				}
			}
			
			// Method 2: Direct type search
			var directSearchGuids = AssetDatabase.FindAssets("t:ServiceKitLocator");
			Debug.Log($"\n[Direct type search 't:ServiceKitLocator'] Found {directSearchGuids.Length} GUID(s):");
			foreach (var guid in directSearchGuids)
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);
				var asset = AssetDatabase.LoadAssetAtPath<ServiceKitLocator>(path);
				Debug.Log($"  • {(asset != null ? asset.name : "null")} at {path}");
			}
			
			// Method 3: ScriptableObject search
			var scriptableObjectGuids = AssetDatabase.FindAssets("t:ScriptableObject");
			var serviceKitLocators = new List<ServiceKitLocator>();
			foreach (var guid in scriptableObjectGuids)
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);
				var asset = AssetDatabase.LoadAssetAtPath<ServiceKitLocator>(path);
				if (asset != null) serviceKitLocators.Add(asset);
			}
			Debug.Log($"\n[ScriptableObject filtered] Found {serviceKitLocators.Count} ServiceKitLocator(s):");
			foreach (var locator in serviceKitLocators)
			{
				var path = AssetDatabase.GetAssetPath(locator);
				Debug.Log($"  • {locator.name} at {path}");
			}
			
			// Method 4: Resources.FindObjectsOfTypeAll (includes loaded assets)
			var allLoadedLocators = Resources.FindObjectsOfTypeAll<ServiceKitLocator>();
			Debug.Log($"\n[Resources.FindObjectsOfTypeAll] Found {allLoadedLocators.Length} ServiceKitLocator(s) (includes loaded):");
			foreach (var locator in allLoadedLocators)
			{
				var path = AssetDatabase.GetAssetPath(locator);
				var isAsset = !string.IsNullOrEmpty(path);
				if (isAsset)
				{
					Debug.Log($"  • {locator.name} at {path}");
				}
				else
				{
					Debug.Log($"  • {locator.name} (runtime instance, not an asset)");
				}
			}
		}
	}
}