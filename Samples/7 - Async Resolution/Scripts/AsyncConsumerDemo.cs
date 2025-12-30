using System;
using System.Threading;
using System.Threading.Tasks;
using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.AsyncResolutionExample
{
    /// <summary>
    /// Demonstrates async service resolution with various patterns:
    /// - GetServiceAsync with timeout
    /// - GetServiceAsync with cancellation
    /// - InjectServicesAsync with configuration
    /// </summary>
    public class AsyncConsumerDemo : MonoBehaviour
    {
        [SerializeField] private ServiceKitLocator _serviceKitLocator;
        [SerializeField] private float _resolutionTimeout = 5f;

        private CancellationTokenSource _cancellationTokenSource;

        private async void Start()
        {
            Debug.Log("[AsyncConsumerDemo] Starting async resolution demos...\n");

            await DemoGetServiceAsync();
            await DemoWithTimeout();
            await DemoWithCancellation();
            await DemoInjectServicesAsync();
        }

        /// <summary>
        /// Basic async service resolution.
        /// </summary>
        private async Task DemoGetServiceAsync()
        {
            Debug.Log("=== Demo: GetServiceAsync ===");

            var startTime = Time.realtimeSinceStartup;

            // Wait for network service to be ready
            var networkService = await _serviceKitLocator.GetServiceAsync<INetworkService>();

            var elapsed = Time.realtimeSinceStartup - startTime;
            Debug.Log($"Network service resolved in {elapsed:F2}s (Connected: {networkService.IsConnected})");

            // Now get dependent services
            var authService = await _serviceKitLocator.GetServiceAsync<IAuthService>();
            Debug.Log($"Auth service resolved (Authenticated: {authService.IsAuthenticated})\n");
        }

        /// <summary>
        /// Resolution with timeout - fails gracefully if service takes too long.
        /// </summary>
        private async Task DemoWithTimeout()
        {
            Debug.Log("=== Demo: WithTimeout ===");

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_resolutionTimeout));

                var cloudSave = await _serviceKitLocator.GetServiceAsync<ICloudSaveService>(cts.Token);

                if (cloudSave != null)
                {
                    Debug.Log($"Cloud save resolved (Ready: {cloudSave.IsReady})");
                    await cloudSave.SaveAsync("demo_key", "demo_value");
                }
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning($"Cloud save resolution timed out after {_resolutionTimeout}s");
            }

            Debug.Log("");
        }

        /// <summary>
        /// Resolution with explicit cancellation.
        /// </summary>
        private async Task DemoWithCancellation()
        {
            Debug.Log("=== Demo: WithCancellation ===");

            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                var resolveTask = _serviceKitLocator.GetServiceAsync<INetworkService>(_cancellationTokenSource.Token);

                // Simulate user cancellation after short delay
                // (In real code, this might be triggered by a cancel button)
                // _cancellationTokenSource.CancelAfter(100);

                var service = await resolveTask;
                Debug.Log($"Resolution completed (Connected: {service.IsConnected})");
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("Resolution was cancelled by user");
            }
            finally
            {
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }

            Debug.Log("");
        }

        /// <summary>
        /// Using InjectServicesAsync with configuration.
        /// </summary>
        private async Task DemoInjectServicesAsync()
        {
            Debug.Log("=== Demo: InjectServicesAsync ===");

            var consumer = new ServiceConsumer();

            try
            {
                await _serviceKitLocator.InjectServicesAsync(consumer)
                    .WithTimeout(_resolutionTimeout)
                    .WithCancellation(destroyCancellationToken)
                    .WithErrorHandling(ex => Debug.LogError($"Injection failed: {ex.Message}"))
                    .ExecuteAsync();

                Debug.Log($"Services injected into consumer");
                consumer.UseServices();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to inject services: {ex.Message}");
            }
        }

        private void OnDestroy()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }

        /// <summary>
        /// Example class that receives injected services.
        /// </summary>
        private class ServiceConsumer
        {
            [InjectService] private INetworkService _networkService;
            [InjectService] private IAuthService _authService;
            [InjectService] private ICloudSaveService _cloudSaveService;

            public void UseServices()
            {
                Debug.Log($"  Network: {(_networkService != null ? "OK" : "NULL")}");
                Debug.Log($"  Auth: {(_authService != null ? "OK" : "NULL")}");
                Debug.Log($"  CloudSave: {(_cloudSaveService != null ? "OK" : "NULL")}");
            }
        }
    }
}
