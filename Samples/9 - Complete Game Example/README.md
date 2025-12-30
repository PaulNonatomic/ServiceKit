# Sample 9: Complete Game Example

A comprehensive example combining all ServiceKit concepts into a cohesive mini-game architecture.

## What You'll Learn

This sample demonstrates all ServiceKit features in a real-world context:
- `[Service]` attribute with multi-type registration
- `[InjectService]` for required and optional dependencies
- ServiceBehaviour lifecycle management
- DontDestroyOnLoad for persistent services
- Event-driven service communication
- Save/load with service tags
- Async service resolution

## Setup Instructions

1. **Create a ServiceKitLocator asset** (if not already created)

2. **Set up the scene hierarchy:**
   ```
   Scene
   ├── --- Global Services (DontDestroyOnLoad) ---
   │   ├── GameStateService
   │   ├── PlayerService
   │   ├── AudioManager
   │   ├── UIService
   │   ├── SaveService
   │   └── AnalyticsService (optional - can remove)
   │
   └── --- Scene Objects ---
       ├── GameBootstrap
       └── GameDemo
   ```

3. **Assign ServiceKitLocator** to all ServiceBehaviour components and GameBootstrap/GameDemo

4. **Play the scene** and use the GameDemo context menu to interact

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                      GameStateService                        │
│  Orchestrates game flow (Menu → Playing → Paused → GameOver)│
├──────────────────┬──────────────────┬───────────────────────┤
│                  │                  │                       │
▼                  ▼                  ▼                       │
┌──────────┐  ┌──────────┐  ┌──────────────┐                  │
│ Player   │  │   UI     │  │    Music     │                  │
│ Service  │  │ Service  │  │   Service    │                  │
└────┬─────┘  └────┬─────┘  └──────────────┘                  │
     │             │              ▲                            │
     │             │              │                            │
     ▼             ▼              │                            │
┌──────────┐  ┌──────────┐  ┌─────┴────────┐  ┌──────────────┐│
│   SFX    │  │  Player  │  │    Audio     │  │  Analytics   ││
│ Service  │  │  Events  │  │   Manager    │  │  (Optional)  ││
└──────────┘  └──────────┘  └──────────────┘  └──────────────┘│
                                                               │
└─────────────────────────────────────────────────────────────┘
```

## Concepts Demonstrated

### 1. Multi-Type Registration (Sample 4)
```csharp
[Service(typeof(IAudioService), typeof(IMusicService), typeof(ISfxService))]
public class AudioManager : ServiceBehaviour, IAudioService, IMusicService, ISfxService
```

### 2. Optional Dependencies (Sample 5)
```csharp
[InjectService(Required = false)]
private IAnalyticsService _analyticsService;

// Safe usage
_analyticsService?.TrackGameStart();
```

### 3. Event-Driven Communication
```csharp
// PlayerService raises events
public event Action<int> OnHealthChanged;
public event Action OnPlayerDied;

// UIService subscribes
_playerService.OnHealthChanged += UpdateHealth;
```

### 4. State Machine Pattern
```csharp
public enum GameState { MainMenu, Playing, Paused, GameOver }

public void SetState(GameState newState)
{
    var oldState = CurrentState;
    CurrentState = newState;
    HandleStateChange(newState);
    OnStateChanged?.Invoke(oldState, newState);
}
```

### 5. Singleton Pattern with ServiceKit
```csharp
protected override void Awake()
{
    if (_instance != null && _instance != this)
    {
        Destroy(gameObject);
        return;
    }
    _instance = this;
    DontDestroyOnLoad(gameObject);
    base.Awake();  // Important: still register with ServiceKit
}
```

## Service Dependency Graph

```
GameStateService
├── IPlayerService
├── IUIService
├── IMusicService
└── IAnalyticsService? (optional)

PlayerService
└── ISfxService

UIService
├── IPlayerService
└── ISfxService

SaveService
└── IPlayerService
```

## Scripts Included

### Interfaces
| Interface | Purpose |
|-----------|---------|
| `IGameStateService` | Game flow management |
| `IPlayerService` | Player state and actions |
| `ISaveService` | Save/load operations |
| `IAudioService` | Master audio control |
| `IMusicService` | Background music |
| `ISfxService` | Sound effects |
| `IUIService` | UI management |
| `IAnalyticsService` | Analytics tracking |

### Implementations
| Service | Notes |
|---------|-------|
| `GameStateService` | Coordinates all other services |
| `PlayerService` | Health, score, events |
| `AudioManager` | Implements 3 interfaces |
| `UIService` | Subscribes to player events |
| `SaveService` | Demonstrates tag-based discovery |
| `AnalyticsService` | Optional - remove to test graceful degradation |

### Controllers
| Script | Purpose |
|--------|---------|
| `GameBootstrap` | Initializes and waits for services |
| `GameDemo` | Interactive demo via context menu |

## Testing the Demo

1. **Play the scene**
2. **Right-click on GameDemo** in Hierarchy
3. **Select actions** from context menu:
   - Start New Game
   - Simulate Gameplay
   - Pause/Resume
   - Save/Load
   - Kill Player (triggers Game Over)
   - Run Full Demo (automated sequence)

## Removing Optional Services

Try removing `AnalyticsService` from the scene:
- Game still works
- No errors
- Analytics calls are safely skipped

This demonstrates the `[InjectService(Required = false)]` pattern.

## Best Practices Demonstrated

1. **Interface Segregation**: Each service has a focused interface
2. **Dependency Injection**: Services receive dependencies automatically
3. **Event-Driven**: Loose coupling through events
4. **Optional Dependencies**: Graceful degradation
5. **Single Responsibility**: Each service has one job
6. **Testability**: Interfaces allow mocking
7. **Persistent Services**: DontDestroyOnLoad pattern

## Extending This Example

Ideas for expansion:
- Add an `IInventoryService` with items
- Add an `IAchievementService` that listens to player events
- Add multiple scenes with scene-local services
- Add async initialization for "connecting" to services

## Summary

This sample shows how all ServiceKit concepts work together:
- Services are decoupled through interfaces
- Dependencies are injected automatically
- Optional services don't break the game
- Events enable loose coupling
- State management is centralized
- Save/load uses service discovery
