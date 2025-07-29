using UnityEngine;

namespace Nonatomic.ServiceKit
{
	/// <summary>
	/// Runtime settings for ServiceKit
	/// </summary>
	[CreateAssetMenu(fileName = "ServiceKitSettings", menuName = "ServiceKit/Settings")]
	public class ServiceKitSettings : ScriptableObject
	{
		public bool AutoCleanupOnSceneUnload => _autoCleanupOnSceneUnload;
		public bool DebugLogging => _debugLogging;
		public bool WarnOnDestroyedRegistration => _warnOnDestroyedRegistration;
		public float DefaultTimeout => _defaultTimeout;
		public ServiceKitLocator DefaultServiceKitLocator => _defaultServiceKitLocator;
		
		private static ServiceKitSettings _instance;
		
		[SerializeField]
		[Tooltip("Automatically unregister MonoBehaviour services when their scene is unloaded")]
		private bool _autoCleanupOnSceneUnload = true;

		[SerializeField]
		[Tooltip("Enable debug logging for service registration/unregistration")]
		private bool _debugLogging = true;

		[SerializeField]
		[Tooltip("Log warnings when services are registered from destroyed objects")]
		private bool _warnOnDestroyedRegistration = true;

		[SerializeField]
		[Tooltip("Maximum time to wait for services before timing out (in seconds)")]
		private float _defaultTimeout = 30f;

		[SerializeField]
		[Tooltip("Default ServiceKitLocator to use for auto-assignment. When set, this takes highest priority over automatic selection.")]
		private ServiceKitLocator _defaultServiceKitLocator;
	
		public static ServiceKitSettings Instance
		{
			get
			{
				if (_instance != null) return _instance;
				
				// First try Resources folder (for runtime access)
				_instance = Resources.Load<ServiceKitSettings>("ServiceKitSettings");
				if (_instance != null) return _instance;

				#if UNITY_EDITOR
				// In editor, also check for any ServiceKitSettings asset in the project
				_instance = FindServiceKitSettingsAsset();
				if (_instance != null) return _instance;
				#endif

				// Fallback to runtime instance
				_instance = CreateInstance<ServiceKitSettings>();
				_instance.name = "ServiceKitSettings (Runtime)";

				#if UNITY_EDITOR
				Debug.LogWarning("[ServiceKit] No ServiceKitSettings asset found. Using runtime instance. " +
								 "Create a ServiceKitSettings asset via Assets → Create → ServiceKit → Settings for persistent settings.");
				#endif
				return _instance;
			}
		}

		#if UNITY_EDITOR
		/// <summary>
		/// Find ServiceKitSettings asset anywhere in the project (Editor only)  
		/// </summary>
		private static ServiceKitSettings FindServiceKitSettingsAsset()
		{
			var guids = UnityEditor.AssetDatabase.FindAssets("t:ServiceKitSettings");
			if (guids.Length == 0) return null;

			// Use the first one found
			var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
			var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<ServiceKitSettings>(path);
			
			if (guids.Length > 1)
			{
				Debug.LogWarning($"[ServiceKit] Multiple ServiceKitSettings assets found. Using: {path}");
			}
			
			return asset;
		}

		/// <summary>
		/// Force refresh the singleton instance (useful after creating new assets)
		/// </summary>
		public static void RefreshInstance()
		{
			_instance = null;
		}
		#endif
		
		#if UNITY_EDITOR
		private void OnValidate()
		{
			_defaultTimeout = Mathf.Max(0.1f, _defaultTimeout);
		}
		#endif
	}
}
