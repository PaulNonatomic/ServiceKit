using System.Collections.Generic;

namespace Nonatomic.ServiceKit.Editor.ServiceKitWindow
{
	public class SceneGroupData
	{
		public string SceneName { get; set; } = "Non-MonoBehaviour";
		public bool IsUnloaded { get; set; } = false;
		public bool IsDontDestroyOnLoad { get; set; } = false;
		public List<ServiceInfo> Services { get; } = new();
	}
}
