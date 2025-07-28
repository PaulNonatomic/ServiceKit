using UnityEditor;
using UnityEngine;

namespace Nonatomic.ServiceKit.Editor
{
	public static class ServiceKitProjectSettings
	{
		private const string AUTO_CLEANUP_KEY = "ServiceKit_AutoCleanupOnSceneUnload";
		private const string DEBUG_LOGGING_KEY = "ServiceKit_DebugLogging";
		private const string DEFAULT_LOCATOR_KEY = "ServiceKit_DefaultLocatorGUID";

		public static bool AutoCleanupOnSceneUnload
		{
			get
			{
				return EditorPrefs.GetBool(AUTO_CLEANUP_KEY, true);
			}
			set
			{
				EditorPrefs.SetBool(AUTO_CLEANUP_KEY, value);
			}
		}

		public static bool DebugLogging
		{
			get
			{
				return EditorPrefs.GetBool(DEBUG_LOGGING_KEY, true);
			}
			set
			{
				EditorPrefs.SetBool(DEBUG_LOGGING_KEY, value);
			}
		}

		/// <summary>
		/// The default ServiceKitLocator to use for auto-assignment.
		/// When set, this takes highest priority over all other selection logic.
		/// </summary>
		public static ServiceKitLocator DefaultServiceKitLocator
		{
			get
			{
				var guid = EditorPrefs.GetString(DEFAULT_LOCATOR_KEY, "");
				if (string.IsNullOrEmpty(guid)) return null;

				var assetPath = AssetDatabase.GUIDToAssetPath(guid);
				if (string.IsNullOrEmpty(assetPath)) return null;

				return AssetDatabase.LoadAssetAtPath<ServiceKitLocator>(assetPath);
			}
			set
			{
				if (value == null)
				{
					EditorPrefs.DeleteKey(DEFAULT_LOCATOR_KEY);
				}
				else
				{
					var assetPath = AssetDatabase.GetAssetPath(value);
					var guid = AssetDatabase.AssetPathToGUID(assetPath);
					EditorPrefs.SetString(DEFAULT_LOCATOR_KEY, guid);
				}
			}
		}

		/// <summary>
		/// Check if a default ServiceKitLocator is explicitly configured
		/// </summary>
		public static bool HasDefaultServiceKitLocator
		{
			get
			{
				var guid = EditorPrefs.GetString(DEFAULT_LOCATOR_KEY, "");
				return !string.IsNullOrEmpty(guid);
			}
		}

		/// <summary>
		/// Clear the default ServiceKitLocator setting
		/// </summary>
		public static void ClearDefaultServiceKitLocator()
		{
			DefaultServiceKitLocator = null;
		}
	}
}
