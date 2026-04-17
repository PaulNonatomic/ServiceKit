using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.CompleteGameExample
{
	/// <summary>
	/// Game bootstrap - initializes all services and starts the game.
	/// </summary>
	public class GameBootstrap : MonoBehaviour
	{
		[SerializeField] private ServiceKitLocator _serviceKitLocator;

		private async void Start()
		{
			Debug.Log("=== Complete Game Example ===\n");
			Debug.Log("[Bootstrap] Waiting for services...\n");

			// Wait for all core services
			var gameState = await _serviceKitLocator.GetServiceAsync<IGameStateService>();
			var player = await _serviceKitLocator.GetServiceAsync<IPlayerService>();
			var ui = await _serviceKitLocator.GetServiceAsync<IUIService>();
			var save = await _serviceKitLocator.GetServiceAsync<ISaveService>();

			Debug.Log("\n[Bootstrap] All services ready!");
			Debug.Log("  - GameStateService: OK");
			Debug.Log("  - PlayerService: OK");
			Debug.Log("  - UIService: OK");
			Debug.Log("  - SaveService: OK");
			Debug.Log($"  - Has save data: {save.HasSaveData}");

			Debug.Log("\n[Bootstrap] Game initialized. Use GameDemo component to interact.\n");
		}
	}
}
