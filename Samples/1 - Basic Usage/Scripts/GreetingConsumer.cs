using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.BasicUsage
{
	/// <summary>
	/// Demonstrates how to consume services from ServiceKit.
	///
	/// This script shows multiple ways to access services:
	/// 1. Synchronous access with GetService<T>()
	/// 2. Safe access with TryGetService<T>()
	/// 3. Status checking with IsServiceReady<T>()
	/// </summary>
	public class GreetingConsumer : MonoBehaviour
	{
		[SerializeField]
		[Tooltip("Reference to the same ServiceKitLocator used for registration")]
		private ServiceKitLocator _serviceKit;

		[SerializeField]
		[Tooltip("The name to use in greetings")]
		private string _playerName = "Player";

		private void Start()
		{
			if (_serviceKit == null)
			{
				Debug.LogError("[GreetingConsumer] ServiceKitLocator is not assigned!");
				return;
			}

			DemonstrateServiceAccess();
		}

		private void DemonstrateServiceAccess()
		{
			// Method 1: Direct access (assumes service is ready)
			// Use when you're certain the service is available
			var greetingService = _serviceKit.GetService<IGreetingService>();
			if (greetingService != null)
			{
				var greeting = greetingService.GetGreeting(_playerName);
				Debug.Log($"[GreetingConsumer] Received: {greeting}");
			}
			else
			{
				Debug.LogWarning("[GreetingConsumer] GreetingService not available!");
			}

			// Method 2: Safe access with TryGetService (recommended)
			// Returns false if service isn't ready, avoiding null checks
			if (_serviceKit.TryGetService<IGreetingService>(out var service))
			{
				var anotherGreeting = service.GetGreeting("World");
				Debug.Log($"[GreetingConsumer] Safe access: {anotherGreeting}");
			}

			// Method 3: Check status before access
			// Useful for conditional logic based on service availability
			if (_serviceKit.IsServiceReady<IGreetingService>())
			{
				Debug.Log("[GreetingConsumer] GreetingService is confirmed ready!");
			}

			// You can also check if a service is registered (but not necessarily ready)
			if (_serviceKit.IsServiceRegistered<IGreetingService>())
			{
				Debug.Log("[GreetingConsumer] GreetingService is registered.");
			}
		}

		/// <summary>
		/// Example of using the service from a UI button click.
		/// </summary>
		public void OnGreetButtonClicked()
		{
			if (_serviceKit.TryGetService<IGreetingService>(out var service))
			{
				var greeting = service.GetGreeting(_playerName);
				Debug.Log($"Button clicked! {greeting}");
			}
		}
	}
}
