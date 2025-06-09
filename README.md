<div align=center>   

<p align="center">
  <img src="Readme~\logo.png" width="500">
</p>

### A flexible & efficient way to manage and access services in <a href="https://unity.com/">Unity</a>

A powerful, ScriptableObject-based service locator pattern implementation for Unity that provides robust dependency injection with asynchronous support, automatic scene management, and comprehensive debugging tools.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![PullRequests](https://img.shields.io/badge/PRs-welcome-blueviolet)](http://makeapullrequest.com)
[![Releases](https://img.shields.io/github/v/release/PaulNonatomic/ServiceKit)](https://github.com/PaulNonatomic/ServiceKit/releases)
[![Unity](https://img.shields.io/badge/Unity-2022.3+-black.svg)](https://unity3d.com/pt/get-unity/download/archive)

</div>
## Features

- **ScriptableObject-Based**: Clean, asset-based architecture that integrates seamlessly with Unity's workflow
- **Async Service Resolution**: Wait for services to become available with cancellation and timeout support
- **Fluent Dependency Injection**: Elegant builder pattern for configuring service injection
- **Automatic Scene Management**: Services are automatically tracked and cleaned up when scenes unload
- **Comprehensive Debugging**: Built-in editor window with search, filtering, and service inspection
- **Type-Safe**: Full generic support with compile-time type checking
- **Performance Optimized**: Efficient service lookup with minimal overhead
- **Thread-Safe**: Concurrent access protection for multi-threaded scenarios

## Installation

### Via Unity Package Manager

1. Open the Package Manager window (`Window > Package Manager`)
2. Click the `+` button and select `Add package from git URL`
3. Enter: `https://github.com/PaulNonatomic/ServiceKit.git`

### Via Package Manager Manifest

Add this line to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.nonatomic.servicekit": "https://github.com/PaulNonatomic/ServiceKit.git"
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
        _serviceKit.RegisterService<IPlayerService>(new PlayerService());
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
        
        RegisterService();
    }
    
    private void Start()
    {
        // Services registered in Awake are now available
        _playerService.LoadPlayer();
        Debug.Log($"Player Level: {_playerService.GetPlayerLevel()}");
    }
}
```

## Advanced Usage

### Using ServiceKitBehaviour Base Class

For common scenarios, inherit from `ServiceKitBehaviour<T>` to automatically handle service injection:

```csharp
public class PlayerController : ServiceKitBehaviour<IPlayerController>
{
    [InjectService] private IPlayerService _playerService;
    [InjectService] private IInventoryService _inventoryService;
    
    // Called automatically after all services are successfully injected
    protected override void OnServicesInjected()
    {
        // Safe to access injected services here - guaranteed to be available
        _playerService.LoadPlayer();
        _inventoryService.LoadInventory();
        
        Debug.Log("Player controller initialized with all dependencies!");
    }
    
    // Optional: Handle injection failures gracefully
    protected override void OnServiceInjectionFailed(Exception exception)
    {
        Debug.LogError($"Failed to initialize player controller: {exception.Message}");
        
        if (exception is TimeoutException)
        {
            // Could retry with different settings
            Debug.Log("Services took too long to become available");
        }
        else if (exception is ServiceInjectionException)
        {
            // Required services are missing
            Debug.Log("Required services are not registered");
            gameObject.SetActive(false); // Disable this component
        }
    }
}
```

The `ServiceKitBehaviour` automatically:
- Injects services during `Awake()`
- Uses default timeout and error handling from settings
- Calls `OnServicesInjected()` only when all services are successfully injected
- Handles errors through the `WithErrorHandling()` fluent API

### Auto-Registration with ServiceKitAutoRegister

Automatically register MonoBehaviours as services:

```csharp
public class AudioManager : ServiceKitAutoRegister<IAudioService>, IAudioService
{
    public void PlaySound(string soundName)
    {
        // Implementation
    }
    
    public void SetMusicVolume(float volume)
    {
        // Implementation
    }
}
```

### Asynchronous Service Resolution

Wait for services that may not be immediately available:

```csharp
public class LateInitializer : MonoBehaviour
{
    [SerializeField] private ServiceKitLocator _serviceKit;
    
    private async void Start()
    {
        try
        {
            // Wait up to 10 seconds for the service
            var audioService = await _serviceKit.GetServiceAsync<IAudioService>(
                new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);
            
            audioService.PlaySound("welcome");
        }
        catch (OperationCanceledException)
        {
            Debug.LogError("Audio service was not available within timeout period");
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
    
    public void ReportEvent(string eventName)
    {
        // Safely handle optional services
        _analyticsService?.TrackEvent(eventName);
        
        // Required services are guaranteed to be available
        var playerLevel = _playerService.GetPlayerLevel();
    }
}
```

### Service Factory Registration

Register services using factory functions:

```csharp
_serviceKit.RegisterServiceFactory<IConfigService>(() => 
{
    var config = new ConfigService();
    config.LoadFromFile("game-config.json");
    return config;
});
```

### Conditional Service Access

Use extension methods for safer service access:

```csharp
// Execute action only if service exists
_serviceKit.WithService<IAudioService>(audio => audio.PlaySound("click"));

// Get result with fallback
var playerLevel = _serviceKit.WithService<IPlayerService>(
    player => player.GetPlayerLevel(), 
    defaultValue: 1);

// Check if service exists
if (_serviceKit.HasService<INetworkService>())
{
    // Handle online features
}
```

## ServiceKit Debug Window

Access the powerful debugging interface via `Tools > ServiceKit > ServiceKit Window`:

### Features:
- **Real-time Service Monitoring**: View all registered services across all ServiceKit locators
- **Scene-based Grouping**: Services organized by the scene that registered them
- **Search & Filtering**: Find services quickly with fuzzy search
- **Service Information**: View registration time, source, and lifecycle status
- **Script Navigation**: Click to open service implementation files
- **GameObject Pinging**: Click MonoBehaviour services to highlight them in the scene

### Color-coded Service Types:
- **Blue**: Non-MonoBehaviour services
- **Orange**: Regular scene services
- **Green**: DontDestroyOnLoad services
- **Red**: Services from unloaded scenes

## Configuration

### ServiceKit Settings

Configure global behavior via `Create > ServiceKit > Settings`:

```csharp
public class ServiceKitSettings : ScriptableSingleton<ServiceKitSettings>
{
    [SerializeField] private bool autoCleanupOnSceneUnload = true;
    [SerializeField] private bool debugLogging = true;
    [SerializeField] private float defaultTimeout = 30f;
    
    // Access via ServiceKitSettings.instance
}
```

### Custom Error Handling

```csharp
await serviceKit.InjectServicesAsync(this)
    .WithErrorHandling(exception => 
    {
        Debug.LogError($"Service injection failed: {exception.Message}");
        // Custom error handling logic
    })
    .ExecuteAsync();
```

## API Reference

### IServiceKitLocator Interface

```csharp
// Registration
void RegisterService<T>(T service, string registeredBy = null) where T : class;
void UnregisterService<T>() where T : class;

// Synchronous Access
T GetService<T>() where T : class;
bool TryGetService<T>(out T service) where T : class;

// Asynchronous Access  
Task<T> GetServiceAsync<T>(CancellationToken cancellationToken = default) where T : class;

// Dependency Injection
IServiceInjectionBuilder InjectServicesAsync(object target);

// Management
void ClearServices();
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
- **Use interfaces** for service contracts to maintain loose coupling
- **Keep services stateless** when possible for better testability
- **Prefer composition** over inheritance for complex service dependencies

### Registration Strategy
- **Register early** in the application lifecycle (Awake/Start)
- **Use scene-specific** ServiceKit locators for scene-scoped services
- **Global services** should be registered in persistent scenes or DontDestroyOnLoad objects

### Dependency Management
- **Mark dependencies as optional** when they're not critical for functionality
- **Use timeouts** for service resolution to avoid indefinite waits
- **Handle injection failures** gracefully with proper error handling

### Testing

```csharp
[Test]
public void TestPlayerServiceIntegration()
{
    var mockServiceKit = Substitute.For<IServiceKitLocator>();
    var playerService = new PlayerService();
    
    mockServiceKit.GetService<IPlayerService>().Returns(playerService);
    
    // Test service interactions
    Assert.AreEqual(1, playerService.GetPlayerLevel());
}
```

## Contributing

We welcome contributions! Please see our [Contributing Guidelines](CONTRIBUTING.md) for details.

### Development Setup
1. Clone the repository
2. Open in Unity 2022.3 or later
3. Run tests via `Window > General > Test Runner`

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Support

- **Documentation**: [Wiki](https://github.com/your-repo/ServiceKit/wiki)
- **Issues**: [GitHub Issues](https://github.com/your-repo/ServiceKit/issues)
- **Discussions**: [GitHub Discussions](https://github.com/your-repo/ServiceKit/discussions)

---

**Built with ❤️ for the Unity community**
