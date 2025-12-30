using System.Collections.Generic;
using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.FluentRegistrationExample
{
    /// <summary>
    /// Bootstrap script demonstrating the fluent registration API.
    ///
    /// This shows how to:
    /// - Use .Register().As&lt;T&gt;().Ready() chain
    /// - Register non-MonoBehaviour services
    /// - Create services with constructor dependencies
    /// - Use .WithTags() for organization
    /// - Understand .Register() vs .Ready() terminal operations
    /// </summary>
    public class FluentBootstrap : MonoBehaviour
    {
        [SerializeField] private ServiceKitLocator _serviceKitLocator;

        private void Start()
        {
            Debug.Log("[FluentBootstrap] Starting service registration...");

            // Step 1: Create and register ConfigService
            // This service has no dependencies, so we create it directly
            var configService = new ConfigService(
                appName: "FluentDemo",
                version: "1.0.0",
                debugMode: true
            );

            _serviceKitLocator.Register(configService)
                .As<IConfigService>()
                .WithTags("core", "config")
                .Ready();

            Debug.Log("[FluentBootstrap] ConfigService registered and ready");

            // Step 2: Create and register LogService
            // This service depends on IConfigService, so we resolve it first
            var logService = new ConsoleLogService(configService);

            _serviceKitLocator.Register(logService)
                .As<ILogService>()
                .WithTags("core", "logging")
                .Ready();

            Debug.Log("[FluentBootstrap] LogService registered and ready");

            // Step 3: Create and register AnalyticsService
            // This service depends on both IConfigService and ILogService
            var analyticsService = new AnalyticsService(configService, logService);

            _serviceKitLocator.Register(analyticsService)
                .As<IAnalyticsService>()
                .WithTags("analytics", "tracking")
                .Ready();

            Debug.Log("[FluentBootstrap] AnalyticsService registered and ready");

            // All services are now registered and ready!
            Debug.Log("[FluentBootstrap] All services registered successfully!");

            // Demonstrate using the services
            DemonstrateServices();
        }

        private void DemonstrateServices()
        {
            Debug.Log("\n--- Demonstrating Services ---\n");

            // Get services from the locator
            var config = _serviceKitLocator.GetService<IConfigService>();
            var log = _serviceKitLocator.GetService<ILogService>();
            var analytics = _serviceKitLocator.GetService<IAnalyticsService>();

            // Use config service
            config.SetValue("volume", 0.8f);
            config.SetValue("language", "en");

            // Use log service
            log.Log("Game started");
            log.LogWarning("Low memory detected");

            // Use analytics service
            analytics.TrackEvent("game_start");
            analytics.TrackEvent("settings_changed", new Dictionary<string, object>
            {
                { "volume", 0.8f },
                { "language", "en" }
            });

            Debug.Log($"\n[FluentBootstrap] Total events tracked: {analytics.TotalEventsTracked}");
        }
    }
}
