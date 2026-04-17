using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.SceneManagementExample
{
	/// <summary>
	/// Gameplay scene controller demonstrating service lifecycle across scenes.
	/// </summary>
	[Service(typeof(GameplaySceneController))]
	public class GameplaySceneController : ServiceKitBehaviour
	{
		// Global services - same instances from menu scene
		[InjectService] private IGlobalService _globalService;
		[InjectService] private ISceneTransitionService _transitionService;

		// Scene-local service - NEW instance for this scene
		[InjectService] private ISceneService _sceneService;

		protected override void InitializeService()
		{
			Debug.Log("[GameplaySceneController] Gameplay initialized");
			Debug.Log($"  - Global service scene count: {_globalService.TotalSceneLoads}");
			Debug.Log($"  - Current scene: {_sceneService.SceneName}");
			Debug.Log($"  - Scene service active: {_sceneService.IsActive}");
		}

		// Called by UI button
		public async void OnBackToMenuClicked()
		{
			Debug.Log("[GameplaySceneController] Returning to menu...");

			// This scene's local services will be cleaned up
			await _transitionService.LoadSceneAsync("MenuScene");
		}

		// Called by UI button
		public async void OnRestartLevelClicked()
		{
			Debug.Log("[GameplaySceneController] Restarting level...");

			// Reload the same scene - creates new scene-local services
			await _transitionService.LoadSceneAsync("GameplayScene");
		}
	}
}
