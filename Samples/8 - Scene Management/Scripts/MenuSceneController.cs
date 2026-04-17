using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.SceneManagementExample
{
	/// <summary>
	/// Menu scene controller that uses both global and scene-local services.
	/// </summary>
	[Service(typeof(MenuSceneController))]
	public class MenuSceneController : ServiceKitBehaviour
	{
		// Global service - persists across scenes
		[InjectService] private IGlobalService _globalService;
		[InjectService] private ISceneTransitionService _transitionService;

		// Scene-local service - destroyed with this scene
		[InjectService] private ISceneService _sceneService;

		protected override void InitializeService()
		{
			Debug.Log("[MenuSceneController] Menu initialized");
			Debug.Log($"  - Global service scene count: {_globalService.TotalSceneLoads}");
			Debug.Log($"  - Current scene: {_sceneService.SceneName}");
		}

		// Called by UI button
		public async void OnPlayButtonClicked()
		{
			Debug.Log("[MenuSceneController] Play button clicked!");

			// Scene-local services will be cleaned up automatically
			await _transitionService.LoadSceneAsync("GameplayScene");
		}

		// Called by UI button
		public void OnQuitButtonClicked()
		{
			Debug.Log("[MenuSceneController] Quit button clicked!");
#if UNITY_EDITOR
			UnityEditor.EditorApplication.isPlaying = false;
#else
			Application.Quit();
#endif
		}
	}
}
