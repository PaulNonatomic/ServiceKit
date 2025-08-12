using System;
using Nonatomic.ServiceKit;
using UnityEngine;

namespace Tests.EditMode
{
	public interface IPlayerService
	{
		void SavePlayer();
		void LoadPlayer();
	}

	public interface IInventoryService
	{
		void AddItem(string itemId);
		void RemoveItem(string itemId);
	}

	public class PlayerService : IPlayerService
	{
		public void SavePlayer()
		{
			Debug.Log("Saving player...");
		}
		public void LoadPlayer()
		{
			Debug.Log("Loading player...");
		}
	}

	public class InventoryService : IInventoryService
	{
		public void AddItem(string itemId)
		{
			Debug.Log($"Adding item: {itemId}");
		}
		public void RemoveItem(string itemId)
		{
			Debug.Log($"Removing item: {itemId}");
		}
	}

	public class GameManager : MonoBehaviour
	{
		[SerializeField] private ServiceKitLocator _serviceKit;

		[InjectService] private IPlayerService _playerService;
		[InjectService] private IInventoryService _inventoryService;

		private async void Awake()
		{
			// Register services (this could be done elsewhere)
			_serviceKit.RegisterService<IPlayerService>(new PlayerService());
			_serviceKit.RegisterService<IInventoryService>(new InventoryService());

			// Inject services
			await _serviceKit.InjectServicesAsync(this)
				.WithCancellation(destroyCancellationToken)
				.WithTimeout(5f)
				.WithErrorHandling(HandleServiceKitError)
				.ExecuteAsync();
		}

		private void HandleServiceKitError(Exception ex)
		{
			Debug.LogError($"ServiceKit error: {ex.Message}");
		}

		private void Start()
		{
			// Services will be injected by this point
			_playerService?.LoadPlayer();
			_inventoryService?.AddItem("sword_001");
		}
	}

    public interface IPlayerController
    {
        IPlayerService PlayerService { get; }
        IInventoryService InventoryService { get; }
    }
    
	public class PlayerController : ServiceKitBehaviour<IPlayerController>, IPlayerController
	{
		public IPlayerService PlayerService => _playerService;
		public IInventoryService InventoryService => _inventoryService;
		
		[InjectService] private IPlayerService _playerService;
		[InjectService] private IInventoryService _inventoryService;
	}

	// Test class that doesn't implement its interface (for testing error scenarios)
	public class BrokenPlayerController : ServiceKitBehaviour<IPlayerController>
	{
		// This class intentionally doesn't implement IPlayerController
		// This should trigger our improved error messages
	}
}
