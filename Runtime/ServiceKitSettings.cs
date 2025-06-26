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
	
		public static ServiceKitSettings Instance
		{
			get
			{
				if (_instance != null) return _instance;
				
				_instance = Resources.Load<ServiceKitSettings>("ServiceKitSettings");
				if (_instance != null) return _instance;

				_instance = CreateInstance<ServiceKitSettings>();
				_instance.name = "ServiceKitSettings (Runtime)";
                        
				#if UNITY_EDITOR
				Debug.LogWarning("ServiceKitSettings: No asset found in Resources folder. Using runtime instance. " +
								 "Consider creating a ServiceKitSettings asset in a Resources folder for persistent settings.");
				#endif
				return _instance;
			}
		}
		
		#if UNITY_EDITOR
		private void OnValidate()
		{
			_defaultTimeout = Mathf.Max(0.1f, _defaultTimeout);
		}
		#endif
	}
}
