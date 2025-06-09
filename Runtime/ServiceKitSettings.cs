using UnityEditor;
using UnityEngine;

namespace Nonatomic.ServiceKit
{
	/// <summary>
	/// Runtime settings for ServiceKit
	/// </summary>
	[CreateAssetMenu(fileName = "ServiceKitSettings", menuName = "ServiceKit/Settings")]
	public class ServiceKitSettings : ScriptableSingleton<ServiceKitSettings>
	{
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

		public bool AutoCleanupOnSceneUnload
		{
			get
			{
				return _autoCleanupOnSceneUnload;
			}
		}
		public bool DebugLogging
		{
			get
			{
				return _debugLogging;
			}
		}
		public bool WarnOnDestroyedRegistration
		{
			get
			{
				return _warnOnDestroyedRegistration;
			}
		}
		public float DefaultTimeout
		{
			get
			{
				return _defaultTimeout;
			}
		}

		#if UNITY_EDITOR
		private void OnValidate()
		{
			//Ensure the timeout is always positive
			_defaultTimeout = Mathf.Max(0.1f, _defaultTimeout);
		}
		#endif
	}
}
