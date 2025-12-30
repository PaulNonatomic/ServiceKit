# Sample 2: ServiceBehaviour

This sample demonstrates the `ServiceBehaviour` base class for creating MonoBehaviour-based services with automatic lifecycle management.

## What You'll Learn

- Using the `[Service]` attribute for type registration
- Automatic registration/unregistration lifecycle
- Dependency injection with `[InjectService]`
- Using `InitializeService()` for post-injection setup
- Handling injection failures with `HandleDependencyInjectionFailure()`

## Setup Instructions

1. **Create a ServiceKitLocator asset** (if not already created)

2. **Set up the scene hierarchy:**
   ```
   Scene
   ├── PlayerService (with PlayerService component)
   ├── ScoreService (with ScoreService component)
   └── GameManager (with GameManager component)
   ```

3. **Assign the ServiceKitLocator** to each ServiceBehaviour component's `Service Kit Locator` field

4. **Play the scene** and call `GameManager.SimulateGameplay()` from the Inspector or another script

## Key Concepts

### The [Service] Attribute
```csharp
// Single interface registration
[Service(typeof(IPlayerService))]
public class PlayerService : ServiceBehaviour, IPlayerService { }

// Multiple interfaces (covered in Sample 4)
[Service(typeof(IAudio), typeof(IMusic))]
public class AudioManager : ServiceBehaviour, IAudio, IMusic { }

// Concrete type (no interface)
[Service(typeof(GameManager))]
public class GameManager : ServiceBehaviour { }
```

### Dependency Injection
```csharp
[Service(typeof(IScoreService))]
public class ScoreService : ServiceBehaviour, IScoreService
{
    // Automatically injected before InitializeService() is called
    [InjectService] private IPlayerService _playerService;

    protected override void InitializeService()
    {
        // _playerService is now available!
        Debug.Log($"Player: {_playerService.PlayerName}");
    }
}
```

### Service Lifecycle

1. **Awake** - ServiceBehaviour registers itself (not ready yet)
2. **Dependency Injection** - Waits for all `[InjectService]` fields
3. **InitializeServiceAsync()** - Your async initialization (optional)
4. **InitializeService()** - Your sync initialization (optional)
5. **Ready** - Service is marked ready, other services can now inject it
6. **OnDestroy** - Service automatically unregisters

### Error Handling
```csharp
protected override void HandleDependencyInjectionFailure(Exception exception)
{
    if (exception is TimeoutException)
    {
        Debug.LogError("Services took too long!");
    }
    else if (exception is ServiceInjectionException)
    {
        Debug.LogError("Required services not available!");
    }
}
```

## Scripts Included

| Script | Purpose |
|--------|---------|
| `IPlayerService.cs` | Player service interface |
| `IScoreService.cs` | Score service interface |
| `PlayerService.cs` | Player service implementation (ServiceBehaviour) |
| `ScoreService.cs` | Score service with dependency on IPlayerService |
| `GameManager.cs` | Consumes both services, demonstrates interactions |

## Dependency Graph

```
GameManager
├── depends on → IPlayerService
└── depends on → IScoreService
                  └── depends on → IPlayerService
```

ServiceKit automatically resolves this dependency graph, ensuring services are initialized in the correct order.

## Next Steps

- **Sample 3**: Learn the fluent registration API
- **Sample 4**: Register one service under multiple interfaces
- **Sample 5**: Handle optional dependencies
