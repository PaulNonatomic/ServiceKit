# Sample 3: Fluent Registration

This sample demonstrates the fluent API for clean, chainable service registration.

## What You'll Learn

- Using `.Register().As<T>().Ready()` chains
- Registering non-MonoBehaviour (plain C#) services
- Constructor-based dependency injection
- Using `.WithTags()` for service organization
- Understanding `.Register()` vs `.Ready()` terminal operations

## Setup Instructions

1. **Create a ServiceKitLocator asset** (if not already created)

2. **Set up the scene hierarchy:**
   ```
   Scene
   └── FluentBootstrap (with FluentBootstrap component)
   ```

3. **Assign the ServiceKitLocator** to the FluentBootstrap component's `Service Kit Locator` field

4. **Play the scene** to see the fluent registration in action

## Key Concepts

### The Fluent Chain
```csharp
_serviceKitLocator.Register(myService)
    .As<IMyInterface>()           // Register as interface type
    .WithTags("core", "network")  // Add organizational tags
    .Ready();                     // Mark as ready (terminal operation)
```

### Non-MonoBehaviour Services
The fluent API is ideal for plain C# classes:
```csharp
// Create service instance with dependencies
var configService = new ConfigService("MyApp", "1.0.0", debugMode: true);

// Register using fluent API
_serviceKitLocator.Register(configService)
    .As<IConfigService>()
    .Ready();
```

### Constructor Dependency Injection
Build dependency graphs manually:
```csharp
// 1. Create base service (no dependencies)
var config = new ConfigService(...);
_serviceKitLocator.Register(config).As<IConfigService>().Ready();

// 2. Create dependent service
var logService = new ConsoleLogService(config);
_serviceKitLocator.Register(logService).As<ILogService>().Ready();

// 3. Create service with multiple dependencies
var analytics = new AnalyticsService(config, logService);
_serviceKitLocator.Register(analytics).As<IAnalyticsService>().Ready();
```

### Register vs Ready

**`.Ready()`** - Register AND mark as ready (most common):
```csharp
// Service is immediately available for injection
_serviceKitLocator.Register(service)
    .As<IMyService>()
    .Ready();
```

**`.Register()`** - Register WITHOUT marking ready:
```csharp
// Register the service (other services can wait for it)
_serviceKitLocator.Register(service)
    .As<IMyService>()
    .Register();

// ... do some async initialization ...

// Later, mark as ready
_serviceKitLocator.ReadyService<IMyService>();
```

### Tags for Organization
```csharp
_serviceKitLocator.Register(service)
    .As<IMyService>()
    .WithTags("core", "network", "saveable")
    .Ready();

// Later, find services by tag
var saveableServices = _serviceKitLocator.GetServicesWithTag("saveable");
```

## Scripts Included

| Script | Purpose |
|--------|---------|
| `IConfigService.cs` | Configuration service interface |
| `ILogService.cs` | Logging service interface |
| `IAnalyticsService.cs` | Analytics tracking interface |
| `ConfigService.cs` | Plain C# config implementation |
| `ConsoleLogService.cs` | Log service with config dependency |
| `AnalyticsService.cs` | Analytics with multiple dependencies |
| `FluentBootstrap.cs` | Bootstrap demonstrating fluent registration |

## Dependency Graph

```
AnalyticsService
├── depends on → IConfigService
└── depends on → ILogService
                 └── depends on → IConfigService
```

## When to Use Fluent vs Attribute Registration

| Use Case | Recommended Approach |
|----------|---------------------|
| MonoBehaviour services | `[Service]` attribute + `ServiceBehaviour` |
| Plain C# classes | Fluent API |
| Services needing constructor injection | Fluent API |
| Simple, self-contained services | Either works well |
| Services created dynamically at runtime | Fluent API |

## Next Steps

- **Sample 4**: Register one service under multiple interfaces
- **Sample 5**: Handle optional dependencies
- **Sample 6**: Use tags for service filtering
