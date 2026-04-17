using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.SceneManagementExample
{
	/// <summary>
	/// Bootstrap scene manager that initializes global services.
	/// This script lives in the Bootstrap scene and sets up persistent services.
	/// </summary>
	public class BootstrapManager : MonoBehaviour
	{
		[SerializeField] private ServiceKitLocator _serviceKitLocator;

		private async void Start()
		{
			Debug.Log("[BootstrapManager] Initializing global services...");

			// Wait for global services to be ready
			var globalService = await _serviceKitLocator.GetServiceAsync<IGlobalService>();
			var transitionService = await _serviceKitLocator.GetServiceAsync<ISceneTransitionService>();

			Debug.Log("[BootstrapManager] Global services ready!");
			Debug.Log($"  - Global Service: {globalService.ServiceName}");
			Debug.Log($"  - Transition Service: {transitionService.CurrentScene}");

			// Transition to main menu (or first game scene)
			Debug.Log("[BootstrapManager] Loading Menu scene...");
			await transitionService.LoadSceneAsync("MenuScene");
		}
	}
}
