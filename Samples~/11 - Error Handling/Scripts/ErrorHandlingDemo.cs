using System;
using System.Threading;
using Nonatomic.ServiceKit;
using UnityEngine;

#if SERVICEKIT_UNITASK
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace ServiceKitSamples.ErrorHandlingExample
{
	/// <summary>
	/// Demonstrates error handling patterns available in ServiceKit.
	///
	/// This is a plain MonoBehaviour (not a ServiceKitBehaviour) that uses the
	/// ServiceKitLocator API directly. It shows four patterns:
	///
	/// 1. TryResolveService - atomic 3-state check (Ready / RegisteredNotReady / NotRegistered)
	/// 2. Timeout handling  - CancellationTokenSource with GetServiceAsync
	/// 3. Custom error handlers - builder pattern with WithErrorHandling
	/// 4. Circular dependency exemption - querying the exemption status
	/// </summary>
	public class ErrorHandlingDemo : MonoBehaviour
	{
		[SerializeField] private ServiceKitLocator _serviceKit;

		private async void Start()
		{
			// Wait a frame to let services begin their registration
#if SERVICEKIT_UNITASK
			await UniTask.Yield();
#else
			await Task.Yield();
#endif

			DemoTryResolveService();
			await DemoTimeoutHandling();
			await DemoCustomErrorHandler();
			DemoCircularDependencyExemption();
		}

		/// <summary>
		/// Demonstrates TryResolveService, which atomically checks the 3 possible states
		/// of a service: Ready, RegisteredNotReady, or NotRegistered.
		///
		/// This is useful when you need to make decisions based on the current state
		/// without waiting for the service to become ready.
		/// </summary>
		private void DemoTryResolveService()
		{
			Debug.Log("=== TryResolveService Demo ===");

			// Check a service that should be registered and possibly ready
			var configStatus = _serviceKit.TryResolveService(typeof(IConfigService), out var configService);
			switch (configStatus)
			{
				case ServiceResolutionStatus.Ready:
					Debug.Log($"  IConfigService is Ready: {((IConfigService)configService).AppName}");
					break;
				case ServiceResolutionStatus.RegisteredNotReady:
					Debug.Log("  IConfigService is registered but still initializing...");
					break;
				case ServiceResolutionStatus.NotRegistered:
					Debug.Log("  IConfigService is not registered.");
					break;
			}

			// Check a slow service (likely RegisteredNotReady at this point)
			var slowStatus = _serviceKit.TryResolveService(typeof(ISlowService), out var slowService);
			switch (slowStatus)
			{
				case ServiceResolutionStatus.Ready:
					Debug.Log($"  ISlowService is Ready: {((ISlowService)slowService).GetData()}");
					break;
				case ServiceResolutionStatus.RegisteredNotReady:
					Debug.Log("  ISlowService is registered but still initializing (expected for slow services).");
					break;
				case ServiceResolutionStatus.NotRegistered:
					Debug.Log("  ISlowService is not registered.");
					break;
			}

			// Check a service type that was never registered
			var missingStatus = _serviceKit.TryResolveService(typeof(IDisposable), out _);
			Debug.Log($"  IDisposable status: {missingStatus} (expected: NotRegistered)");
		}

		/// <summary>
		/// Demonstrates timeout handling with GetServiceAsync.
		///
		/// By passing a CancellationToken with a timeout, you can limit how long
		/// the caller waits for a service to become ready. If the timeout expires,
		/// an OperationCanceledException is thrown, allowing you to use a fallback.
		/// </summary>
#if SERVICEKIT_UNITASK
		private async UniTask DemoTimeoutHandling()
#else
		private async Task DemoTimeoutHandling()
#endif
		{
			Debug.Log("=== Timeout Handling Demo ===");

			// Try to get SlowService with a short timeout (1 second).
			// Since SlowService takes 2 seconds to initialize, this will time out.
			try
			{
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
				var slow = await _serviceKit.GetServiceAsync<ISlowService>(cts.Token);
				Debug.Log($"  SlowService resolved: {slow.GetData()}");
			}
			catch (OperationCanceledException)
			{
				Debug.LogWarning("  SlowService timed out after 1s - using fallback behavior.");
			}

			// Now wait with a longer timeout that should succeed
			try
			{
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
				var slow = await _serviceKit.GetServiceAsync<ISlowService>(cts.Token);
				Debug.Log($"  SlowService resolved on second attempt: {slow.GetData()}");
			}
			catch (OperationCanceledException)
			{
				Debug.LogWarning("  SlowService timed out even with 5s timeout.");
			}
		}

		/// <summary>
		/// Demonstrates the builder pattern for injection with custom error handling.
		///
		/// The Inject() builder supports:
		/// - WithTimeout(seconds) to set a maximum wait time
		/// - WithErrorHandling(handler) to provide a custom error callback
		/// - WithCancellation(token) to link cancellation
		/// - ExecuteWithCancellationAsync(token) as a shorthand
		/// </summary>
#if SERVICEKIT_UNITASK
		private async UniTask DemoCustomErrorHandler()
#else
		private async Task DemoCustomErrorHandler()
#endif
		{
			Debug.Log("=== Custom Error Handler Demo ===");

			await _serviceKit.Inject(this)
				.WithCancellation(destroyCancellationToken)
				.WithTimeout(2f)
				.WithErrorHandling(ex =>
				{
					if (ex is TimeoutException)
					{
						Debug.LogWarning("  Injection timed out - continuing without optional services.");
					}
					else if (ex is OperationCanceledException)
					{
						Debug.LogWarning("  Injection cancelled.");
					}
					else
					{
						Debug.LogError($"  Injection failed: {ex.Message}");
					}
				})
				.ExecuteAsync();

			Debug.Log("  Custom error handler demo complete.");
		}

		/// <summary>
		/// Demonstrates how to query circular dependency exemption status.
		///
		/// CircularServiceB is registered with CircularDependencyExempt = true,
		/// which allows it to participate in a circular dependency without triggering
		/// ServiceKit's cycle detection.
		/// </summary>
		private void DemoCircularDependencyExemption()
		{
			Debug.Log("=== Circular Dependency Exemption Demo ===");

			var isAExempt = _serviceKit.IsServiceCircularDependencyExempt<ICircularA>();
			var isBExempt = _serviceKit.IsServiceCircularDependencyExempt<ICircularB>();

			Debug.Log($"  ICircularA exempt: {isAExempt} (expected: False)");
			Debug.Log($"  ICircularB exempt: {isBExempt} (expected: True)");

			// Check overall service status
			Debug.Log($"  ICircularA status: {_serviceKit.GetServiceStatus<ICircularA>()}");
			Debug.Log($"  ICircularB status: {_serviceKit.GetServiceStatus<ICircularB>()}");
		}
	}
}
