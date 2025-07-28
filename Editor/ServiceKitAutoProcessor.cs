using System;
using System.Collections.Generic;
using System.Linq;
using Nonatomic.ServiceKit.Editor.Utils;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nonatomic.ServiceKit.Editor
{
	/// <summary>
	/// Automatically processes ServiceKitLocator references during asset import and project changes
	/// This ensures auto-assignment happens without requiring inspector viewing
	/// </summary>
	[InitializeOnLoad]
	public static class ServiceKitAutoProcessor
	{
		static ServiceKitAutoProcessor()
		{
			EditorApplication.projectChanged += OnProjectChanged;
			EditorApplication.hierarchyChanged += OnHierarchyChanged;
		}

		/// <summary>
		/// Called when project assets change - process new/modified assets for auto-assignment
		/// </summary>
		private static void OnProjectChanged()
		{
			// Delay the processing to ensure assets are fully loaded
			EditorApplication.delayCall += () =>
			{
				ProcessPendingAutoAssignments();
			};
		}

		/// <summary>
		/// Called when hierarchy changes - process scene objects for auto-assignment
		/// </summary>
		private static void OnHierarchyChanged()
		{
			// Only process in edit mode to avoid runtime interference
			if (!EditorApplication.isPlaying)
			{
				EditorApplication.delayCall += () =>
				{
					ProcessSceneAutoAssignments();
				};
			}
		}

		/// <summary>
		/// Process pending auto-assignments for project assets
		/// </summary>
		private static void ProcessPendingAutoAssignments()
		{
			var defaultLocator = GetDefaultServiceKitLocator();
			if (defaultLocator == null) return;

			var processedCount = 0;
			
			// Find all loaded prefabs and process them
			var allObjects = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
			
			foreach (var obj in allObjects)
			{
				if (obj == null) continue;
				
				// Skip objects that are not assets (scene objects will be handled separately)
				if (!EditorUtility.IsPersistent(obj)) continue;

				var serializedObject = new SerializedObject(obj);
				if (ProcessObjectForAutoAssignment(serializedObject, defaultLocator))
				{
					serializedObject.ApplyModifiedProperties();
					EditorUtility.SetDirty(obj);
					processedCount++;
				}
			}

			if (processedCount > 0)
			{
				AssetDatabase.SaveAssets();
			}
		}

		/// <summary>
		/// Process scene objects for auto-assignment
		/// </summary>
		private static void ProcessSceneAutoAssignments()
		{
			var defaultLocator = GetDefaultServiceKitLocator();
			if (defaultLocator == null) return;

			var processedCount = 0;
			var allObjects = Object.FindObjectsOfType<MonoBehaviour>();

			foreach (var obj in allObjects)
			{
				if (obj == null) continue;

				var serializedObject = new SerializedObject(obj);
				if (ProcessObjectForAutoAssignment(serializedObject, defaultLocator))
				{
					serializedObject.ApplyModifiedProperties();
					EditorUtility.SetDirty(obj);
					processedCount++;
				}
			}

			// No need to save assets for scene objects - they're saved with the scene
		}

		/// <summary>
		/// Process a single object for ServiceKitLocator auto-assignment
		/// </summary>
		private static bool ProcessObjectForAutoAssignment(SerializedObject serializedObject, ServiceKitLocator defaultLocator)
		{
			var modified = false;
			var property = serializedObject.GetIterator();

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
			if (targetObject == null) return null;
			
			var targetType = targetObject.GetType();
			
			// Navigate to the field through the property path
			var fieldInfo = targetType.GetField(property.name, 
				System.Reflection.BindingFlags.Instance | 
				System.Reflection.BindingFlags.Public | 
				System.Reflection.BindingFlags.NonPublic);
			
			return fieldInfo?.FieldType;
		}

		/// <summary>
		/// Manual trigger for processing all objects in the project
		/// </summary>
		[MenuItem("Tools/ServiceKit/Process All Auto-Assignments")]
		public static void ProcessAllAutoAssignments()
		{
			ProcessPendingAutoAssignments();
			ProcessSceneAutoAssignments();
			
			Debug.Log("[ServiceKit] Processed all auto-assignments for ServiceKitLocator references.");
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
	}
}