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

			// If no locators found, show default object field with warning
			if (_cachedLocators == null || _cachedLocators.Count == 0)
			{
				DrawNoLocatorsFound(position, property, label);
				EditorGUI.EndProperty();
				return;
			}

			// Auto-assign first locator if field is null
			if (property.objectReferenceValue == null && _cachedLocators.Count > 0)
			{
				property.objectReferenceValue = _cachedLocators[0];
				property.serializedObject.ApplyModifiedProperties();
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
				// Sort by name for consistent ordering
				_cachedLocators = _cachedLocators.OrderBy(l => l.name).ToList();
				
				// Create display names (show path for duplicates)
				_cachedDisplayNames = new string[_cachedLocators.Count];
				var nameGroups = _cachedLocators.GroupBy(l => l.name).ToList();
				
				for (int i = 0; i < _cachedLocators.Count; i++)
				{
					var locator = _cachedLocators[i];
					var group = nameGroups.First(g => g.Key == locator.name);
					
					if (group.Count() > 1)
					{
						// Multiple locators with same name - show path
						var path = AssetDatabase.GetAssetPath(locator);
						var folderPath = System.IO.Path.GetDirectoryName(path)?.Replace("Assets/", "") ?? "";
						_cachedDisplayNames[i] = $"{locator.name} ({folderPath})";
					}
					else
					{
						// Unique name - just show the name
						_cachedDisplayNames[i] = locator.name;
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
		}
	}
}