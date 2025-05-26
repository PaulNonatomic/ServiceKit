using UnityEditor;
using UnityEngine;

namespace Nonatomic.ServiceKit.Editor
{
	public class ServiceKitSettingsProvider : SettingsProvider
	{
		public ServiceKitSettingsProvider() : base("Project/ServiceKit", SettingsScope.Project)
		{
			label = "ServiceKit";
		}

		public override void OnGUI(string searchContext)
		{
			EditorGUILayout.LabelField("ServiceKit Settings", EditorStyles.boldLabel);
			EditorGUILayout.Space();

			ServiceKitProjectSettings.AutoCleanupOnSceneUnload = EditorGUILayout.Toggle(
				new GUIContent("Auto Cleanup on Scene Unload",
					"Automatically unregister MonoBehaviour services when their scene is unloaded"),
				ServiceKitProjectSettings.AutoCleanupOnSceneUnload);

			ServiceKitProjectSettings.DebugLogging = EditorGUILayout.Toggle(
				new GUIContent("Debug Logging",
					"Enable debug logging for service registration/unregistration"),
				ServiceKitProjectSettings.DebugLogging);
		}
	}
}
