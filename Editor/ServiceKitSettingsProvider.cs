using System.Collections.Generic;
using System.Linq;
using Nonatomic.ServiceKit.Editor.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nonatomic.ServiceKit.Editor
{
	/// <summary>
	/// Project Settings provider for ServiceKit configuration
	/// </summary>
	public class ServiceKitSettingsProvider : SettingsProvider
	{
		private const string SETTINGS_PATH = "Project/ServiceKit";

		public ServiceKitSettingsProvider(string path, SettingsScope scope = SettingsScope.Project)
			: base(path, scope) { }

		public override void OnGUI(string searchContext)
		{
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("ServiceKit Configuration", EditorStyles.boldLabel);
			EditorGUILayout.Space();

			DrawDefaultLocatorSection();
			EditorGUILayout.Space();
			DrawGeneralSettings();
		}

		/// <summary>
		/// Draw the default ServiceKitLocator selection section
		/// </summary>
		private void DrawDefaultLocatorSection()
		{
			EditorGUILayout.LabelField("Auto-Assignment", EditorStyles.boldLabel);
			
			EditorGUI.indentLevel++;

			// Get all available ServiceKitLocators
			var allLocators = AssetUtils.FindAssetsByType<ServiceKitLocator>();
			var settingsDefault = ServiceKitSettings.Instance.DefaultServiceKitLocator;
			var currentDefault = ServiceKitProjectSettings.DefaultServiceKitLocator;

			// Show priority information
			EditorGUILayout.HelpBox(
				"Priority Order:\n" +
				"1. ServiceKitSettings asset (visible in inspector)\n" +
				"2. Project Settings configuration (below)\n" +
				"3. Package-included ServiceKit (most predictable)\n" +
				"4. Automatic selection (naming/location-based)",
				MessageType.Info
			);
			EditorGUILayout.Space();

			if (allLocators == null || allLocators.Count == 0)
			{
				EditorGUILayout.HelpBox("No ServiceKitLocators found in project. Create one first.", MessageType.Warning);
				
				if (GUILayout.Button("Create Default ServiceKitLocator"))
				{
					var newLocator = ScriptableObjectUtils.CreateInstanceInProject<ServiceKitLocator>(
						fileName: "Default ServiceKit",
						selectInstance: true
					);
					
					if (newLocator != null)
					{
						ServiceKitProjectSettings.DefaultServiceKitLocator = newLocator;
						Debug.Log($"[ServiceKit] Created and set '{newLocator.name}' as default ServiceKitLocator");
					}
				}
			}
			else
			{
				// Show ServiceKitSettings status first (highest priority)
				EditorGUILayout.LabelField("ServiceKitSettings Asset Configuration:", EditorStyles.miniBoldLabel);
				
				var settingsInstance = ServiceKitSettings.Instance;
				var isRuntimeInstance = settingsInstance.name.Contains("Runtime");
				
				if (isRuntimeInstance)
				{
					EditorGUILayout.HelpBox("⚠ Using runtime ServiceKitSettings instance. Create an asset for persistent settings.", MessageType.Warning);
					if (GUILayout.Button("Create ServiceKitSettings Asset"))
					{
						ServiceKitLocatorAutoAssigner.OpenServiceKitSettings();
					}
				}
				else if (settingsDefault != null)
				{
					EditorGUILayout.BeginHorizontal();
					EditorGUILayout.LabelField($"✓ Active Default: {settingsDefault.name}", EditorStyles.boldLabel);
					if (GUILayout.Button("Ping Asset", EditorStyles.miniButton, GUILayout.Width(80)))
					{
						EditorGUIUtility.PingObject(settingsInstance);
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.HelpBox("This ServiceKitLocator is set in the ServiceKitSettings asset and has highest priority.", MessageType.Info);
				}
				else
				{
					EditorGUILayout.BeginHorizontal();
					EditorGUILayout.LabelField("⚠ No default set in ServiceKitSettings asset", EditorStyles.label);
					if (GUILayout.Button("Open Settings Asset", EditorStyles.miniButton))
					{
						EditorGUIUtility.PingObject(settingsInstance);
						Selection.activeObject = settingsInstance;
					}
					EditorGUILayout.EndHorizontal();
				}

				EditorGUILayout.Space();

				// Show current Project Settings default (lower priority)
				EditorGUILayout.LabelField("Project Settings Configuration (Fallback):", EditorStyles.miniBoldLabel);
				
				var newDefault = EditorGUILayout.ObjectField(
					"Default Locator", 
					currentDefault, 
					typeof(ServiceKitLocator), 
					false
				) as ServiceKitLocator;

				if (newDefault != currentDefault)
				{
					ServiceKitProjectSettings.DefaultServiceKitLocator = newDefault;
					
					if (newDefault != null)
					{
						Debug.Log($"[ServiceKit] Set '{newDefault.name}' as default ServiceKitLocator for auto-assignment");
					}
					else
					{
						Debug.Log("[ServiceKit] Cleared default ServiceKitLocator - will use automatic selection");
					}
				}

				// Show available locators for reference
				if (allLocators.Count > 1)
				{
					EditorGUILayout.Space(5);
					EditorGUILayout.LabelField($"Available ServiceKitLocators ({allLocators.Count}):", EditorStyles.miniLabel);
					
					EditorGUI.indentLevel++;
					foreach (var locator in allLocators.OrderBy(l => l.name))
					{
						var path = AssetDatabase.GetAssetPath(locator);
						var folderPath = System.IO.Path.GetDirectoryName(path)?.Replace("Assets/", "") ?? "";
						var displayText = string.IsNullOrEmpty(folderPath) ? locator.name : $"{locator.name} ({folderPath})";
						
						EditorGUILayout.BeginHorizontal();
						EditorGUILayout.LabelField("•", GUILayout.Width(10));
						
						if (GUILayout.Button(displayText, EditorStyles.linkLabel, GUILayout.ExpandWidth(true)))
						{
							EditorGUIUtility.PingObject(locator);
						}
						
						if (locator == currentDefault)
						{
							EditorGUILayout.LabelField("(Default)", EditorStyles.miniLabel, GUILayout.Width(60));
						}
						else if (GUILayout.Button("Set Default", EditorStyles.miniButton, GUILayout.Width(80)))
						{
							ServiceKitProjectSettings.DefaultServiceKitLocator = locator;
							Debug.Log($"[ServiceKit] Set '{locator.name}' as default ServiceKitLocator");
						}
						
						EditorGUILayout.EndHorizontal();
					}
					EditorGUI.indentLevel--;
				}

				// Clear button
				if (currentDefault != null)
				{
					EditorGUILayout.Space(5);
					if (GUILayout.Button("Clear Default (Use Automatic Selection)", EditorStyles.miniButton))
					{
						ServiceKitProjectSettings.ClearDefaultServiceKitLocator();
						Debug.Log("[ServiceKit] Cleared default ServiceKitLocator - will use automatic selection");
					}
				}

				// Help text
				EditorGUILayout.Space(5);
				EditorGUILayout.HelpBox(
					"When set, this ServiceKitLocator will be automatically assigned to all empty ServiceKitLocator fields " +
					"in prefabs and scenes without requiring inspector viewing. " +
					"If not set, ServiceKit will use smart automatic selection based on naming and location.",
					MessageType.Info
				);

				// Batch operations
				EditorGUILayout.Space(10);
				EditorGUILayout.LabelField("Batch Operations:", EditorStyles.miniBoldLabel);
				EditorGUILayout.BeginHorizontal();
				
				if (GUILayout.Button("Auto-Assign All", EditorStyles.miniButton))
				{
					ServiceKitLocatorAutoAssigner.AutoAssignAllServiceKitLocators();
				}
				
				if (GUILayout.Button("Process Auto-Assignments", EditorStyles.miniButton))
				{
					ServiceKitAutoProcessor.ProcessAllAutoAssignments();
				}
				
				EditorGUILayout.EndHorizontal();
			}

			EditorGUI.indentLevel--;
		}

		/// <summary>
		/// Draw general ServiceKit settings
		/// </summary>
		private void DrawGeneralSettings()
		{
			EditorGUILayout.LabelField("General Settings", EditorStyles.boldLabel);
			
			EditorGUI.indentLevel++;

			var newAutoCleanup = EditorGUILayout.Toggle(
				new GUIContent("Auto Cleanup on Scene Unload", "Automatically unregister services when scenes are unloaded"),
				ServiceKitProjectSettings.AutoCleanupOnSceneUnload
			);
			if (newAutoCleanup != ServiceKitProjectSettings.AutoCleanupOnSceneUnload)
			{
				ServiceKitProjectSettings.AutoCleanupOnSceneUnload = newAutoCleanup;
			}

			var newDebugLogging = EditorGUILayout.Toggle(
				new GUIContent("Debug Logging", "Enable debug logging for ServiceKit operations"),
				ServiceKitProjectSettings.DebugLogging
			);
			if (newDebugLogging != ServiceKitProjectSettings.DebugLogging)
			{
				ServiceKitProjectSettings.DebugLogging = newDebugLogging;
			}

			EditorGUI.indentLevel--;
		}

		[SettingsProvider]
		public static SettingsProvider CreateServiceKitSettingsProvider()
		{
			var provider = new ServiceKitSettingsProvider(SETTINGS_PATH, SettingsScope.Project)
			{
				label = "ServiceKit",
				keywords = GetSearchKeywordsFromGUIContentProperties<Styles>()
			};

			return provider;
		}

		private class Styles
		{
			public static readonly GUIContent autoCleanup = new GUIContent("Auto Cleanup");
			public static readonly GUIContent debugLogging = new GUIContent("Debug Logging");
			public static readonly GUIContent defaultLocator = new GUIContent("Default ServiceKit Locator");
			public static readonly GUIContent autoAssignment = new GUIContent("Auto Assignment");
		}
	}
}