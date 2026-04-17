using System;
using System.Threading.Tasks;
using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.AsyncResolutionExample
{
	/// <summary>
	/// Demonstrates proper timeout handling patterns.
	/// </summary>
	[Service(typeof(TimeoutDemo))]
	public class TimeoutDemo : ServiceKitBehaviour
	{
		[InjectService] private INetworkService _networkService;

		protected override void InitializeService()
		{
			Debug.Log("[TimeoutDemo] Service initialized with network dependency");
		}

		/// <summary>
		/// Called when dependency injection times out.
		/// </summary>
		protected override void HandleDependencyInjectionFailure(Exception exception)
		{
			if (exception is TimeoutException)
			{
				Debug.LogError("[TimeoutDemo] Service dependencies timed out!");
				// Handle timeout - maybe show error UI
				HandleTimeout();
			}
			else if (exception is OperationCanceledException)
			{
				Debug.LogWarning("[TimeoutDemo] Service injection was cancelled");
				// Handle cancellation - normal during shutdown
			}
			else
			{
				Debug.LogError($"[TimeoutDemo] Unexpected error: {exception.Message}");
				// Handle other errors
			}
		}

		private void HandleTimeout()
		{
			Debug.Log("[TimeoutDemo] Attempting recovery from timeout...");
			// Options:
			// 1. Retry with backoff
			// 2. Use fallback/offline mode
			// 3. Show error to user
		}
	}
}
