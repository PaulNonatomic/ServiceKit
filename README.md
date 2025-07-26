<div align=center>   

<p align="center">
  <img src="Readme~\logo.png" width="500">
</p>

### A flexible & efficient way to manage and access services in [Unity](https://unity.com/)

A powerful, ScriptableObject-based service locator pattern implementation for Unity that provides robust dependency injection with asynchronous support, automatic scene management, and comprehensive debugging tools.

</div>

## Features

-   **ScriptableObject-Based**: Clean, asset-based architecture that integrates seamlessly with Unity's workflow.
-   **Multi-Phase Initialization**: A robust, automated lifecycle ensures services are registered, injected, and initialized safely.
-   **Async Service Resolution**: Wait for services to become fully ready with cancellation and timeout support.
-   **Fluent Dependency Injection**: Elegant builder pattern for configuring service injection.
-   **Automatic Scene Management**: Services are automatically tracked and cleaned up when scenes unload.
-   **Comprehensive Debugging**: Built-in editor window with search, filtering, and service inspection.
-   **Type-Safe**: Full generic support with compile-time type checking.
-   **Performance Optimized**: Efficient service lookup with minimal overhead.
-   **Thread-Safe**: Concurrent access protection for multi-threaded scenarios.

## Installation

### Via Unity Package Manager

1.  Open the Package Manager window (`Window > Package Manager`)
2.  Click the `+` button and select `Add package from git URL`
3.  Enter: `https://github.com/PaulNonatomic/ServiceKit.git`

### Via Package Manager Manifest

Add this line to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.nonatomic.servicekit": "[https://github.com/PaulNonatomic/ServiceKit.git](https://github.com/PaulNonatomic/ServiceKit.git)"
  }
}
```

## Quick Start

### 1. Create a ServiceKit Locator

Right-click in your project window and create a ServiceKit Locator:
`Create > ServiceKit > ServiceKitLocator`

### 2. Define Your Services

```csharp
public interface IPlayerService
{
    void SavePlayer();
    void LoadPlayer();
    int GetPlayerLevel();
}

public class PlayerService : IPlayerService
{
    private int _playerLevel = 1;

    public void SavePlayer() => Debug.Log("Player saved!");
    public void LoadPlayer() => Debug.Log("Player loaded!");
    public int GetPlayerLevel() => _playerLevel;
}
```

### 3. Register Services

```csharp
public class GameBootstrap : MonoBehaviour
{
    [SerializeField] private ServiceKitLocator _serviceKit;

    private void Awake()
    {
        // Register services during game startup
        var playerService = new PlayerService();
        _serviceKit.RegisterService<IPlayerService>(playerService);

        // A service must be marked as "Ready" before it can be injected
        _serviceKit.ReadyService<IPlayerService>();
    }
}
```

### 4. Inject Services

```csharp
public class PlayerUI : MonoBehaviour
{
    [SerializeField] private ServiceKitLocator _serviceKit;

    // Mark fields for injection
    [InjectService] private IPlayerService _playerService;

    private async void Awake()
    {
        // Inject services with fluent configuration
        await _serviceKit.InjectServicesAsync(this)
            .WithTimeout(5f)
            .WithCancellation(destroyCancellationToken)
            .WithErrorHandling()
            .ExecuteAsync();

        // The service is now injected and ready to use
        _playerService.LoadPlayer();
        Debug.Log($"Player Level: {_playerService.GetPlayerLevel()}");
    }
}
```

## Advanced Usage

### Using `ServiceKitBehaviour` Base Class

For the most robust and seamless experience, inherit from `ServiceKitBehaviour<T>`. This base class automates a sophisticated multi-phase initialization process within a single `Awake()` call, ensuring that services are registered, injected, and made ready in a safe, deterministic order.

It handles the following lifecycle automatically:
1.  **Registration**: The service immediately registers itself, making it *discoverable*.
2.  **Dependency Injection**: It asynchronously waits for all services marked with `[InjectService]` to become fully *ready*.
3.  **Custom Initialization**: It provides `InitializeServiceAsync()` and `InitializeService()` for you to override with your own setup logic.
4.  **Readiness**: After your initialization, it marks the service as *ready*, allowing other services that depend on it to complete their own initialization.

```csharp
public class PlayerController : ServiceKitBehaviour<IPlayerController>, IPlayerController
{
    [InjectService] private IPlayerService _playerService;
    [InjectService] private IInventoryService _inventoryService;

    // This is the new hook for your initialization logic.
    // It's called after dependencies are injected, but before this service is marked as "Ready".
    protected override void InitializeService()
    {
        // Safe to access injected services here
        _playerService.LoadPlayer();
        _inventoryService.LoadInventory();

        Debug.Log("Player controller initialized with all dependencies!");
    }

    // For async setup, you can use the async override:
    protected override async Task InitializeServiceAsync()
    {
        // Example: load data from a web request or file
        await _inventoryService.LoadFromCloudAsync(destroyCancellationToken);
    }

    // Optional: Handle injection failures gracefully
    protected override void OnServiceInjectionFailed(Exception exception)
    {
        Debug.LogError($"Failed to initialize player controller: {exception.Message}");

        if (exception is TimeoutException)
        {
            Debug.Log("Services took too long to become available");
        }
        else if (exception is ServiceInjectionException)
        {
            Debug.Log("Required services are not registered or failed to become ready");
            gameObject.SetActive(false); // Disable this component
        }
    }
}
```

### Asynchronous Service Resolution

Wait for services that may not be immediately available (or ready):

```csharp
public class LateInitializer : MonoBehaviour
{
    [SerializeField] private ServiceKitLocator _serviceKit;

    private async void Start()
    {
        try
        {
            // Wait up to 10 seconds for the service to be registered AND ready
            var audioService = await _serviceKit.GetServiceAsync<IAudioService>(
                new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);

            audioService.PlaySound("welcome");
        }
        catch (OperationCanceledException)
        {
            Debug.LogError("Audio service was not available or ready within the timeout period");
        }
    }
}
```

### Optional Dependencies

Services can be marked as optional and won't cause injection to fail if unavailable:

```csharp
public class AnalyticsReporter : MonoBehaviour
{
    [InjectService(Required = false)]
    private IAnalyticsService _analyticsService; // Won't fail if missing

    [InjectService]
    private IPlayerService _playerService; // Required - will fail if missing

    // ...
}
```

### Exempting Services from Circular Dependency Checks

In advanced scenarios, you might need to bypass the circular dependency check. This is useful for two main reasons:

1.  **Wrapping Third-Party Code**: When you "shim" an external library into ServiceKit, that service has no knowledge of your project's classes. A circular dependency check is unnecessary and can be safely bypassed.
2.  **Managed Deadlocks**: In rare cases, a "manager" service might need a reference to a "subordinate" that also depends back on the manager. If you can guarantee this cycle is not accessed until after full initialization, an exemption can resolve the deadlock.

To handle this, you can register a service with an exemption. **This should be used with extreme caution**, as it bypasses a critical safety feature.

`RegisterServiceWithCircularExemption` allows a service to be registered without being considered in the dependency graph analysis.

```csharp
// Example of a service that needs to be exempted
public class SubordinateService : ServiceKitBehaviour<ISubordinateService>
{
    [InjectService] private IManagerService _manager;

    // Override Awake to change the registration method
    protected override void Awake()
    {
        // Instead of the default registration, we call the exemption method.
        ServiceKitLocator.RegisterServiceWithCircularExemption<ISubordinateService>(this);
        Registered = true; // Manually set the flag since we overrode the default
    }
    //...
}
```

## ServiceKit Debug Window

Access the powerful debugging interface via `Tools > ServiceKit > ServiceKit Window`:

### Features:

* **Real-time Service Monitoring**: View all registered services across all ServiceKit locators.
* **Readiness Status**: See at a glance whether a service is just registered or fully ready.
* **Scene-based Grouping**: Services organized by the scene that registered them.
* **Search & Filtering**: Find services quickly with fuzzy search.
* **Script Navigation**: Click to open service implementation files.
* **GameObject Pinging**: Click MonoBehaviour services to highlight them in the scene.

## API Reference

### IServiceKitLocator Interface

```csharp
// Registration & Readiness
void RegisterService<T>(T service, string registeredBy = null) where T : class;
void RegisterServiceWithCircularExemption<T>(T service, string registeredBy = null) where T : class;
void ReadyService<T>() where T : class;
void UnregisterService<T>() where T : class;

// Synchronous Access
T GetService<T>() where T : class;
bool TryGetService<T>(out T service) where T : class;

// Asynchronous Access
Task<T> GetServiceAsync<T>(CancellationToken cancellationToken = default) where T : class;

// Dependency Injection
IServiceInjectionBuilder InjectServicesAsync(object target);

// Management
IReadOnlyList<ServiceInfo> GetAllServices();
```

### IServiceInjectionBuilder Interface

```csharp
IServiceInjectionBuilder WithCancellation(CancellationToken cancellationToken);
IServiceInjectionBuilder WithTimeout(float timeoutSeconds);
IServiceInjectionBuilder WithErrorHandling(Action<Exception> errorHandler);
void Execute(); // Fire-and-forget
Task ExecuteAsync(); // Awaitable
```

## Best Practices

### Service Design

* **Use interfaces** for service contracts to maintain loose coupling.
* **Keep services stateless** when possible for better testability.
* **Prefer composition** over inheritance for complex service dependencies.

### Registration Strategy

* **Register early** in the application lifecycle. `ServiceKitBehaviour` automates this in `Awake`.
* **Initialize wisely**. Place dependency-related logic in `InitializeService` or `InitializeServiceAsync` when using `ServiceKitBehaviour`.
* **Global services** should be registered in persistent scenes or DontDestroyOnLoad objects.

### Dependency Management

* **Mark dependencies as optional** when they're not critical for functionality.
* **Use timeouts** for service resolution to avoid indefinite waits.
* **Handle injection failures** gracefully with proper error handling.
* **Avoid circular dependency exemptions** unless absolutely necessary and the lifecycle is fully understood.

## Contributing

We welcome contributions! Please see our [Contributing Guidelines](CONTRIBUTING.md) for details.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

**Built with ❤️ for the Unity community**

