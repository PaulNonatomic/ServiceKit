using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.BasicUsage
{
	/// <summary>
	/// Demonstrates basic service registration using ServiceKit.
	///
	/// This bootstrap script shows how to:
	/// 1. Create a service instance
	/// 2. Register it with the ServiceKitLocator
	/// 3. Mark it as ready for consumption
	///
	/// In a real game, this would typically be in a persistent bootstrap scene
	/// or attached to a DontDestroyOnLoad object.
	/// </summary>
	public class GameBootstrap : MonoBehaviour
	{
		[SerializeField]
		[Tooltip("Reference to the ServiceKitLocator asset. Create one via: Create > ServiceKit > ServiceKitLocator")]
		private ServiceKitLocator _serviceKit;

		private void Awake()
		{
			if (_serviceKit == null)
			{
				Debug.LogError("[GameBootstrap] ServiceKitLocator is not assigned! " +
					"Please assign a ServiceKitLocator asset in the Inspector.");
				return;
			}

			RegisterServices();
		}

		private void RegisterServices()
		{
			// Register and ready in one fluent chain
			_serviceKit.Register(new GreetingService())
				.As<IGreetingService>()
				.Ready();

			Debug.Log("[GameBootstrap] Services registered and ready!");
		}
	}
}
