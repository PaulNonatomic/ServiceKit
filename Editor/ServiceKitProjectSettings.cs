using UnityEditor;

namespace Nonatomic.ServiceKit.Editor
{
	public static class ServiceKitProjectSettings
	{
		private const string AUTO_CLEANUP_KEY = "ServiceKit_AutoCleanupOnSceneUnload";
		private const string DEBUG_LOGGING_KEY = "ServiceKit_DebugLogging";

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
	}
}
