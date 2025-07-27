#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Nonatomic.ServiceKit.Editor
{
	/// <summary>
	/// Handles clearing ServiceKitLocator instances when exiting play mode in the editor.
	/// This prevents services from persisting between play sessions.
	/// </summary>
	[InitializeOnLoad]
	public static class ServiceKitPlayModeHandler
	{
		static ServiceKitPlayModeHandler()
		{
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
		}

		private static void OnPlayModeStateChanged(PlayModeStateChange state)
		{
			if (state == PlayModeStateChange.ExitingPlayMode)
			{
				ClearAllServiceKitLocators();
			}
		}

		private static void ClearAllServiceKitLocators()
		{
			// Find all ServiceKitLocator assets in the project
			var guids = AssetDatabase.FindAssets("t:ServiceKitLocator");
			var clearedCount = 0;

			foreach (var guid in guids)
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);
				var locator = AssetDatabase.LoadAssetAtPath<ServiceKitLocator>(path);

				if (locator != null)
				{
					locator.ClearServices();
					clearedCount++;
				}
			}

			if (clearedCount > 0)
			{
				Debug.Log($"[ServiceKit] Cleared {clearedCount} ServiceKitLocator(s) on exiting play mode.");
			}
		}
	}
}
#endif