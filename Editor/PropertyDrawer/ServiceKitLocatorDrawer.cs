using System;
using System.Collections.Generic;
using System.Linq;
using Nonatomic.ServiceKit.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace Nonatomic.ServiceKit.Editor.PropertyDrawer
{
	[CustomPropertyDrawer(typeof(ServiceKitLocator))]
	public class ServiceKitLocatorDrawer : UnityEditor.PropertyDrawer
	{
		private static List<ServiceKitLocator> _cachedLocators;
		private static string[] _cachedDisplayNames;
		private static bool _cacheValid = false;

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);

			// Refresh cache if needed
			if (!_cacheValid || _cachedLocators == null)
			{
				RefreshCache();
			}

			// IMPROVED: Auto-assign first locator if field is null (moved to ensure it works without GUI drawing)
			EnsureAutoAssignment(property);

			// If no locators found, show default object field with warning
			if (_cachedLocators == null || _cachedLocators.Count == 0)
			{
				DrawNoLocatorsFound(position, property, label);
				EditorGUI.EndProperty();
				return;
			}

			// Find current selection index
			var currentLocator = property.objectReferenceValue as ServiceKitLocator;
			var selectedIndex = currentLocator != null 
				? _cachedLocators.IndexOf(currentLocator) 
				: 0;

			// If current selection not found in list, reset to first
			if (selectedIndex < 0 && _cachedLocators.Count > 0)
			{
				selectedIndex = 0;
				property.objectReferenceValue = _cachedLocators[0];
			}

			// Draw the dropdown
			var newIndex = EditorGUI.Popup(position, label.text, selectedIndex, _cachedDisplayNames);

			// Update property if selection changed
			if (newIndex != selectedIndex && newIndex >= 0 && newIndex < _cachedLocators.Count)
			{
				property.objectReferenceValue = _cachedLocators[newIndex];
			}

			EditorGUI.EndProperty();
		}

		private void DrawNoLocatorsFound(Rect position, SerializedProperty property, GUIContent label)
		{
			var originalColor = GUI.color;
			
			// Split the rect for the object field and create button
			var objectFieldRect = new Rect(position.x, position.y, position.width - 60, position.height);
			var createButtonRect = new Rect(position.x + position.width - 55, position.y, 55, position.height);

			// Draw object field with warning color
			GUI.color = Color.yellow;
			EditorGUI.ObjectField(objectFieldRect, property, typeof(ServiceKitLocator), label);
			GUI.color = originalColor;

			// Draw create button
			if (GUI.Button(createButtonRect, "Create"))
			{
				CreateNewServiceKitLocator(property);
			}

			// Show warning message
			var warningRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + 2, 
				position.width, EditorGUIUtility.singleLineHeight);
			
			var warningStyle = new GUIStyle(EditorStyles.helpBox)
			{
				fontSize = 10,
				normal = { textColor = Color.yellow }
			};
			
			GUI.Label(warningRect, "⚠ No ServiceKitLocators found in project. Click 'Create' to make one.", warningStyle);
		}

		private void CreateNewServiceKitLocator(SerializedProperty property)
		{
			var newLocator = ScriptableObjectUtils.CreateInstanceInProject<ServiceKitLocator>(selectInstance: false);
			if (newLocator != null)
			{
				property.objectReferenceValue = newLocator;
				property.serializedObject.ApplyModifiedProperties();
				
				// Refresh cache to include the new locator
				RefreshCache();
				
				Debug.Log($"Created new ServiceKitLocator: {newLocator.name}");
			}
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			// If no locators found, add extra height for warning message
			if (!_cacheValid || _cachedLocators == null || _cachedLocators.Count == 0)
			{
				return EditorGUIUtility.singleLineHeight * 2 + 4; // Extra line for warning
			}
			
			return EditorGUIUtility.singleLineHeight;
		}

		private static void RefreshCache()
		{
			_cachedLocators = AssetUtils.FindAssetsByType<ServiceKitLocator>();
			
			if (_cachedLocators != null && _cachedLocators.Count > 0)
			{
				// Sort by priority: configured default first, then alphabetical
				var defaultLocator = GetStaticPriorityBasedDefaultLocator();
				
				// Remove default from list if it exists, then add it at the beginning
				if (defaultLocator != null && _cachedLocators.Contains(defaultLocator))
				{
					_cachedLocators.Remove(defaultLocator);
					_cachedLocators.Insert(0, defaultLocator);
					
					// Sort the rest alphabetically
					var remaining = _cachedLocators.Skip(1).OrderBy(l => l.name).ToList();
					_cachedLocators = new List<ServiceKitLocator> { defaultLocator };
					_cachedLocators.AddRange(remaining);
				}
				else
				{
					// No configured default, just sort by name for consistent ordering
					_cachedLocators = _cachedLocators.OrderBy(l => l.name).ToList();
				}
				
				// Create display names (show path for duplicates and mark default)
				_cachedDisplayNames = new string[_cachedLocators.Count];
				var nameGroups = _cachedLocators.GroupBy(l => l.name).ToList();
				var configuredDefault = GetStaticPriorityBasedDefaultLocator();
				
				for (int i = 0; i < _cachedLocators.Count; i++)
				{
					var locator = _cachedLocators[i];
					var group = nameGroups.First(g => g.Key == locator.name);
					
					string baseName;
					if (group.Count() > 1)
					{
						// Multiple locators with same name - show path
						var path = AssetDatabase.GetAssetPath(locator);
						var folderPath = System.IO.Path.GetDirectoryName(path)?.Replace("Assets/", "") ?? "";
						baseName = $"{locator.name} ({folderPath})";
					}
					else
					{
						// Unique name - just show the name
						baseName = locator.name;
					}
					
					// Mark the configured default
					if (configuredDefault != null && locator == configuredDefault)
					{
						_cachedDisplayNames[i] = $"★ {baseName} (Default)";
					}
					else
					{
						_cachedDisplayNames[i] = baseName;
					}
				}
			}
			else
			{
				_cachedDisplayNames = new string[0];
			}
			
			_cacheValid = true;
		}

		// Static method to invalidate cache when assets change
		[InitializeOnLoadMethod]
		private static void Initialize()
		{
			EditorApplication.projectChanged += InvalidateCache;
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
			
			// Also invalidate cache when ServiceKitSettings instance changes
			ServiceKitSettings.RefreshInstance();
		}

		private static void OnPlayModeStateChanged(PlayModeStateChange state)
		{
			if (state == PlayModeStateChange.EnteredEditMode)
			{
				InvalidateCache();
			}
		}

		private static void InvalidateCache()
		{
			_cacheValid = false;
			// Also refresh the ServiceKitSettings instance to pick up any changes
			ServiceKitSettings.RefreshInstance();
		}

		/// <summary>
		/// Ensure auto-assignment happens regardless of whether GUI is drawn
		/// </summary>
		private void EnsureAutoAssignment(SerializedProperty property)
		{
			if (property.objectReferenceValue == null && _cachedLocators != null && _cachedLocators.Count > 0)
			{
				// Use priority-based selection instead of just first in cache
				var defaultLocator = GetPriorityBasedDefaultLocator();
				property.objectReferenceValue = defaultLocator;
				property.serializedObject.ApplyModifiedProperties();
				
				// Force the asset to be dirty so it gets saved
				EditorUtility.SetDirty(property.serializedObject.targetObject);
			}
		}

		/// <summary>
		/// Static method to perform auto-assignment without requiring GUI drawing
		/// Called by ServiceKitLocatorAutoAssigner and during asset import
		/// </summary>
		public static bool TryAutoAssignServiceKitLocator(SerializedProperty property)
		{
			if (property.objectReferenceValue != null) return false;
			if (property.propertyType != SerializedPropertyType.ObjectReference) return false;
			
			// Use priority-based selection
			var defaultLocator = GetStaticPriorityBasedDefaultLocator();
			
			// If no configured default, fall back to cache
			if (defaultLocator == null)
			{
				// Refresh cache if needed
				if (!_cacheValid || _cachedLocators == null)
				{
					RefreshCache();
				}

				if (_cachedLocators != null && _cachedLocators.Count > 0)
				{
					defaultLocator = _cachedLocators[0];
				}
			}

			if (defaultLocator != null)
			{
				property.objectReferenceValue = defaultLocator;
				return true;
			}

			return false;
		}

		/// <summary>
		/// Batch auto-assign ServiceKitLocators to all objects in the project
		/// </summary>
		[MenuItem("Tools/ServiceKit/Auto-Assign All ServiceKit Locators")]
		public static void AutoAssignAllServiceKitLocators()
		{
			ServiceKitLocatorAutoAssigner.AutoAssignAllServiceKitLocators();
		}

		/// <summary>
		/// Open ServiceKit project settings
		/// </summary>
		[MenuItem("Tools/ServiceKit/Project Settings")]
		public static void OpenProjectSettings()
		{
			SettingsService.OpenProjectSettings("Project/ServiceKit");
		}

		/// <summary>
		/// Get the default ServiceKitLocator using priority-based selection logic
		/// </summary>
		private ServiceKitLocator GetPriorityBasedDefaultLocator()
		{
			return GetStaticPriorityBasedDefaultLocator() ?? (_cachedLocators?.FirstOrDefault());
		}

		/// <summary>
		/// Static version of priority-based default locator selection (for use in cache refresh)
		/// </summary>
		private static ServiceKitLocator GetStaticPriorityBasedDefaultLocator()
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

			// PRIORITY 1: Package-included ServiceKit (for consistent fallback)
			var allLocators = AssetUtils.FindAssetsByType<ServiceKitLocator>();
			if (allLocators != null && allLocators.Count > 0)
			{
				var packageServiceKit = GetPackageIncludedServiceKit(allLocators);
				if (packageServiceKit != null)
				{
					return packageServiceKit;
				}
			}

			return null;
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