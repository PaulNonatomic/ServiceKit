using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
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
			EditorGUILayout.Space();
			DrawDeveloperTools();
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

		/// <summary>
		/// Draw developer tools section including Roslyn Analyzers
		/// </summary>
		private void DrawDeveloperTools()
		{
			EditorGUILayout.LabelField("Developer Tools", EditorStyles.boldLabel);
			
			EditorGUI.indentLevel++;

			DrawRoslynAnalyzersSection();

			EditorGUI.indentLevel--;
		}

		/// <summary>
		/// Draw the Roslyn Analyzers section
		/// </summary>
		private void DrawRoslynAnalyzersSection()
		{
			EditorGUILayout.LabelField("Roslyn Analyzers", EditorStyles.miniBoldLabel);
			
			var analyzerPath = Path.Combine(Application.dataPath, "Analyzers", "ServiceKit");
			var dllPath = Path.Combine(analyzerPath, "ServiceKit.Analyzers.dll");
			var analyzerExists = File.Exists(dllPath);

			if (analyzerExists)
			{
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField("✓ Analyzer DLL Available", EditorStyles.boldLabel);
				if (GUILayout.Button("Show in Explorer", EditorStyles.miniButton, GUILayout.Width(120)))
				{
					EditorUtility.RevealInFinder(analyzerPath);
				}
				EditorGUILayout.EndHorizontal();
				
				var fileInfo = new FileInfo(dllPath);
				EditorGUILayout.LabelField($"Size: {fileInfo.Length / 1024:N0} KB", EditorStyles.miniLabel);
				EditorGUILayout.LabelField($"Modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm}", EditorStyles.miniLabel);
				
				EditorGUILayout.HelpBox(
					"ServiceKit Roslyn Analyzers are available and ready to provide code analysis and suggestions for ServiceKit development.",
					MessageType.Info
				);
			}
			else
			{
				EditorGUILayout.HelpBox(
					"ServiceKit Roslyn Analyzers are not installed. Click the button below to download the latest analyzer DLL from GitHub releases.",
					MessageType.Warning
				);
			}

			EditorGUILayout.BeginHorizontal();
			
			if (GUILayout.Button(analyzerExists ? "Update Analyzers" : "Download Analyzers", EditorStyles.miniButton))
			{
				DownloadAnalyzersAsync();
			}
			
			if (analyzerExists && GUILayout.Button("Remove Analyzers", EditorStyles.miniButton))
			{
				if (EditorUtility.DisplayDialog("Remove ServiceKit Analyzers", 
					"Are you sure you want to remove the ServiceKit Roslyn Analyzers?", 
					"Remove", "Cancel"))
				{
					RemoveAnalyzers();
				}
			}
			
			EditorGUILayout.EndHorizontal();
		}

		/// <summary>
		/// Download the ServiceKit Analyzers from GitHub releases
		/// </summary>
		private async void DownloadAnalyzersAsync()
		{
			const string downloadUrl = "https://github.com/PaulNonatomic/ServiceKitAnalyzers/releases/latest/download/ServiceKit.Analyzers.dll";
			
			try
			{
				EditorUtility.DisplayProgressBar("ServiceKit Analyzers", "Downloading analyzers...", 0.1f);
				
				var analyzerPath = Path.Combine(Application.dataPath, "Analyzers", "ServiceKit");
				var dllPath = Path.Combine(analyzerPath, "ServiceKit.Analyzers.dll");
				
				// Create directory if it doesn't exist
				if (!Directory.Exists(analyzerPath))
				{
					Directory.CreateDirectory(analyzerPath);
				}

				EditorUtility.DisplayProgressBar("ServiceKit Analyzers", "Downloading from GitHub...", 0.3f);
				
				var downloadResult = await DownloadFileFromUrl(downloadUrl, dllPath);
				
				EditorUtility.DisplayProgressBar("ServiceKit Analyzers", "Refreshing Asset Database...", 0.9f);
				
				// Refresh the Asset Database so Unity recognizes the new file
				if (downloadResult)
				{
					AssetDatabase.Refresh();
					ConfigureAnalyzerDllSettings(dllPath);
				}
				
				EditorUtility.DisplayProgressBar("ServiceKit Analyzers", "Download complete", 1.0f);
				EditorUtility.ClearProgressBar();
				
				if (downloadResult)
				{
					Debug.Log($"[ServiceKit] Successfully downloaded ServiceKit.Analyzers to {dllPath}");
					EditorUtility.DisplayDialog("Success", "ServiceKit Roslyn Analyzers have been downloaded successfully!", "OK");
				}
				else
				{
					Debug.LogError("[ServiceKit] Failed to download ServiceKit.Analyzers");
					EditorUtility.DisplayDialog("Error", "Failed to download ServiceKit Analyzers. Check the Console for details.", "OK");
				}
			}
			catch (System.Exception ex)
			{
				EditorUtility.ClearProgressBar();
				Debug.LogError($"[ServiceKit] Exception while downloading analyzers: {ex}");
				EditorUtility.DisplayDialog("Error", $"Exception occurred: {ex.Message}", "OK");
			}
		}

		/// <summary>
		/// Download a file from URL to local path
		/// </summary>
		private async Task<bool> DownloadFileFromUrl(string url, string localPath)
		{
			try
			{
				using (var client = new HttpClient())
				{
					client.Timeout = System.TimeSpan.FromMinutes(5);
					
					var response = await client.GetAsync(url);
					
					if (response.IsSuccessStatusCode)
					{
						var content = await response.Content.ReadAsByteArrayAsync();
						await File.WriteAllBytesAsync(localPath, content);
						return true;
					}
					else
					{
						Debug.LogError($"[ServiceKit] HTTP error downloading analyzer: {response.StatusCode} - {response.ReasonPhrase}");
						return false;
					}
				}
			}
			catch (System.Exception ex)
			{
				Debug.LogError($"[ServiceKit] Failed to download analyzer: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Remove the analyzer DLL files
		/// </summary>
		private void RemoveAnalyzers()
		{
			try
			{
				var analyzerPath = Path.Combine(Application.dataPath, "Analyzers", "ServiceKit");
				var dllPath = Path.Combine(analyzerPath, "ServiceKit.Analyzers.dll");

				if (File.Exists(dllPath))
				{
					File.Delete(dllPath);
					AssetDatabase.Refresh();
				}

				Debug.Log("[ServiceKit] Removed ServiceKit Analyzer DLL");
				EditorUtility.DisplayDialog("Success", "ServiceKit Analyzer DLL has been removed.", "OK");
			}
			catch (System.Exception ex)
			{
				Debug.LogError($"[ServiceKit] Failed to remove analyzer DLL: {ex}");
				EditorUtility.DisplayDialog("Error", $"Failed to remove analyzer DLL: {ex.Message}", "OK");
			}
		}

		/// <summary>
		/// Configure the analyzer DLL import settings to work as a Roslyn analyzer
		/// </summary>
		private void ConfigureAnalyzerDllSettings(string dllPath)
		{
			try
			{
				EditorUtility.DisplayProgressBar("ServiceKit Analyzers", "Configuring DLL settings...", 0.95f);
				
				// Convert absolute path to relative path from Assets folder
				var relativePath = "Assets" + dllPath.Substring(Application.dataPath.Length).Replace('\\', '/');
				
				// Get the asset importer for the DLL
				var importer = AssetImporter.GetAtPath(relativePath) as PluginImporter;
				
				if (importer != null)
				{
					// Critical: Disable auto reference - this prevents Unity from automatically referencing the DLL
					importer.SetCompatibleWithAnyPlatform(false);
					importer.SetCompatibleWithEditor(false);
					
					// Disable all platform targets to ensure it's not included in builds
					importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows, false);
					importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, false);
					importer.SetCompatibleWithPlatform(BuildTarget.StandaloneLinux64, false);
					importer.SetCompatibleWithPlatform(BuildTarget.StandaloneOSX, false);
					importer.SetCompatibleWithPlatform(BuildTarget.Android, false);
					importer.SetCompatibleWithPlatform(BuildTarget.iOS, false);
					importer.SetCompatibleWithPlatform(BuildTarget.WebGL, false);
					
					// Additional settings to ensure it's treated as analyzer only
					importer.isPreloaded = false;
					
					// Save these settings first
					importer.SaveAndReimport();
					
					// Now directly modify the meta file to ensure proper analyzer configuration
					ConfigureMetaFileAsAnalyzer(relativePath);
					
					Debug.Log("[ServiceKit] Configured ServiceKit.Analyzers.dll as Roslyn analyzer with all platforms and auto-reference disabled");
				}
				else
				{
					Debug.LogError($"[ServiceKit] Could not find PluginImporter for {relativePath}");
				}
			}
			catch (System.Exception ex)
			{
				Debug.LogError($"[ServiceKit] Failed to configure analyzer DLL settings: {ex}");
			}
		}

		/// <summary>
		/// Configure the meta file to mark the DLL as a Roslyn analyzer and disable auto reference
		/// </summary>
		private void ConfigureMetaFileAsAnalyzer(string relativePath)
		{
			try
			{
				var metaPath = relativePath + ".meta";
				
				// Read the current meta file content
				if (File.Exists(metaPath))
				{
					var metaContent = File.ReadAllText(metaPath);
					
					// Replace the entire meta file with a properly configured one for Roslyn analyzers
					var newMetaContent = GenerateAnalyzerMetaFile();
					
					File.WriteAllText(metaPath, newMetaContent);
					AssetDatabase.Refresh();
					
					Debug.Log($"[ServiceKit] Completely reconfigured meta file for Roslyn analyzer with disabled auto-reference: {metaPath}");
				}
			}
			catch (System.Exception ex)
			{
				Debug.LogError($"[ServiceKit] Failed to configure meta file as analyzer: {ex}");
			}
		}

		/// <summary>
		/// Generate a complete meta file configuration for a Roslyn analyzer
		/// </summary>
		private string GenerateAnalyzerMetaFile()
		{
			return @"fileFormatVersion: 2
guid: " + System.Guid.NewGuid().ToString("N") + @"
PluginImporter:
  externalObjects: {}
  serializedVersion: 2
  iconMap: {}
  executionOrder: {}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 0
  platformData:
  - first:
      Any: 
    second:
      enabled: 0
      settings:
        RoslynAnalyzer: 1
  - first:
      Editor: Editor
    second:
      enabled: 0
      settings:
        RoslynAnalyzer: 1
        DefaultValueInitialized: true
  - first:
      Windows Store Apps: WindowsStoreApps
    second:
      enabled: 0
      settings:
        CPU: AnyCPU
  userData: 
  assetBundleName: 
  assetBundleVariant: ";
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