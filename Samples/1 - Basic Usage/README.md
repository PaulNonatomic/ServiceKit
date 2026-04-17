# Sample 1: Basic Usage

This sample introduces the core concepts of ServiceKit - creating a ServiceKitLocator, registering services, and consuming them.

## What You'll Learn

- Creating a ServiceKitLocator asset
- Defining service interfaces and implementations
- Registering services with the fluent API `.Register().As<T>().Ready()`
- Accessing services with `GetService<T>()` and `TryGetService<T>()`
- Checking service status with `IsServiceReady<T>()`

## Setup Instructions

1. **Create a ServiceKitLocator asset:**
   - Right-click in the Project window
   - Select `Create > ServiceKit > ServiceKitLocator`
   - Name it "SampleServiceKit"

2. **Set up the scene:**
   - Create an empty GameObject named "Bootstrap"
   - Add the `GameBootstrap` component
   - Assign the ServiceKitLocator asset to the `Service Kit` field

3. **Add a consumer:**
   - Create another GameObject named "Consumer"
   - Add the `GreetingConsumer` component
   - Assign the same ServiceKitLocator asset
   - Optionally change the `Player Name` field

4. **Play the scene** and observe the Console for greeting messages.

## Key Concepts

### Service Interface
```csharp
public interface IGreetingService
{
    string GetGreeting(string name);
}
```

### Service Implementation
```csharp
public class GreetingService : IGreetingService
{
    public string GetGreeting(string name)
    {
        return $"Hello, {name}!";
    }
}
```

### Registration
```csharp
// Fluent API — register and ready in one chain
_serviceKit.Register(new GreetingService())
    .As<IGreetingService>()
    .Ready();
```

### Consumption
```csharp
// Direct access
var service = _serviceKit.GetService<IGreetingService>();

// Safe access (recommended)
if (_serviceKit.TryGetService<IGreetingService>(out var service))
{
    service.GetGreeting("Player");
}
```

## Scripts Included

| Script | Purpose |
|--------|---------|
| `IGreetingService.cs` | Service interface definition |
| `GreetingService.cs` | Service implementation |
| `GameBootstrap.cs` | Registers services at startup |
| `GreetingConsumer.cs` | Demonstrates service consumption |

## Next Steps

- **Sample 2**: Learn about `ServiceKitBehaviour` for MonoBehaviour-based services
- **Sample 3**: Explore the fluent registration API
