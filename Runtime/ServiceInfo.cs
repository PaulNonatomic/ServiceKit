using System;
using System.Collections.Generic;

namespace Nonatomic.ServiceKit
{
	[Serializable]
	public class ServiceInfo
	{
		/// <summary>
		/// The actual service instance
		/// </summary>
		public object Service { get; set; }

		/// <summary>
		/// The primary type used for service registration (usually the interface type)
		/// </summary>
		public Type PrimaryType { get; set; }

		/// <summary>
		/// All types this service is registered under (primary + additional types)
		/// </summary>
		public IReadOnlyList<Type> AllTypes { get; set; } = Array.Empty<Type>();

		/// <summary>
		/// The type of the service (usually the interface type)
		/// For backward compatibility - same as PrimaryType
		/// </summary>
		public Type ServiceType
		{
			get => PrimaryType;
			set => PrimaryType = value;
		}

		/// <summary>
		/// Custom tags associated with this service (e.g. "ThirdParty", "Firebase", "Debug")
		/// </summary>
		public List<ServiceTag> Tags { get; set; } = new ();

#if UNITY_EDITOR
		/// <summary>
		/// Editor-only debug metadata for ServiceKit Window and debugging
		/// </summary>
		public ServiceInfoDebugData DebugData { get; set; } = new();
#endif
	}

#if UNITY_EDITOR
	/// <summary>
	/// Editor-only debug information for services (used by ServiceKit Window)
	/// </summary>
	[Serializable]
	public class ServiceInfoDebugData
	{
		/// <summary>
		/// The name of the scene where the service was registered
		/// </summary>
		public string SceneName { get; set; }
		
		/// <summary>
		/// The handle of the scene (used for tracking scene unloading)
		/// </summary>
		public int SceneHandle { get; set; }
		
		/// <summary>
		/// Whether this service is in the DontDestroyOnLoad scene
		/// </summary>
		public bool IsDontDestroyOnLoad { get; set; }
		
		/// <summary>
		/// When the service was registered
		/// </summary>
		public DateTime RegisteredAt { get; set; }
		
		/// <summary>
		/// The method name that called RegisterService (uses CallerMemberName)
		/// </summary>
		public string RegisteredBy { get; set; }
		
		/// <summary>
		/// Current state of the service: "Registered" or "Ready"
		/// </summary>
		public string State { get; set; }
	}
#endif
}