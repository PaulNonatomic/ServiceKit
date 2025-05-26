using System;

namespace Nonatomic.ServiceKit
{
	[Serializable]
	public class ServiceInfo
	{
		public object Service { get; set; }
		public Type ServiceType { get; set; }
		public string SceneName { get; set; }
		public int SceneHandle { get; set; }
		public bool IsDontDestroyOnLoad { get; set; }
		public DateTime RegisteredAt { get; set; }
		public string RegisteredBy { get; set; }
	}
}
