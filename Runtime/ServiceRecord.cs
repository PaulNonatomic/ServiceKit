using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nonatomic.ServiceKit
{
	public class ServiceRecord
	{
		public object Instance { get; }
		public Scene OwningScene { get; }
		
		public ServiceRecord(object instance)
		{
			Instance = instance;
			
			if (instance is MonoBehaviour monoBehaviour)
			{
				// Check if the MonoBehaviour is actually part of a scene
				if (monoBehaviour.gameObject != null && monoBehaviour.gameObject.scene.IsValid())
				{
					OwningScene = monoBehaviour.gameObject.scene;
				}
				else
				{
					// MonoBehaviour might not be in a scene (e.g., prefab asset, or not fully initialized)
					OwningScene = default; // Explicitly set to default invalid scene
				}
			}
			else
			{
				OwningScene = default; // Not a MonoBehaviour, so no scene
			}
		}
	}
}