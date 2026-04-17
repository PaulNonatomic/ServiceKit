# Sample 8: Scene Management

This sample demonstrates managing service lifecycle across scene transitions.

## What You'll Learn

- Creating global services that persist across scenes
- Creating scene-local services that clean up automatically
- Using DontDestroyOnLoad with ServiceKitBehaviour
- Proper scene transition handling
- Service availability across scenes

## Scene Setup

### Bootstrap Scene (BootstrapScene)
The first scene that loads - initializes global services.
```
Scene
├── ServiceKitLocator (ScriptableObject in scene)
├── GlobalServices (Empty GameObject, DontDestroyOnLoad)
│   ├── GlobalPersistentService
│   └── SceneTransitionService
└── BootstrapManager
```

### Menu Scene (MenuScene)
```
Scene
├── SceneLocalService
└── MenuSceneController
```

### Gameplay Scene (GameplayScene)
```
Scene
├── SceneLocalService (new instance!)
└── GameplaySceneController
```

## Key Concepts

### Global Services (Persist Across Scenes)
```csharp
[Service(typeof(IGlobalService))]
public class GlobalPersistentService : ServiceKitBehaviour, IGlobalService
{
    private static GlobalPersistentService _instance;

    protected override void Awake()
    {
        // Singleton pattern
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        base.Awake();  // Register with ServiceKit
    }
}
```

### Scene-Local Services (Auto-Cleanup)
```csharp
[Service(typeof(ISceneService))]
public class SceneLocalService : ServiceKitBehaviour, ISceneService
{
    // No DontDestroyOnLoad = destroyed when scene unloads
    // ServiceKitBehaviour.OnDestroy() automatically unregisters

    protected override void OnDestroy()
    {
        Debug.Log("Scene service being cleaned up");
        base.OnDestroy();  // Unregisters from ServiceKit
    }
}
```

### Using Both Service Types
```csharp
[Service(typeof(GameplayController))]
public class GameplayController : ServiceKitBehaviour
{
    // Global - same instance across all scenes
    [InjectService] private IGlobalService _globalService;

    // Scene-local - new instance per scene
    [InjectService] private ISceneService _sceneService;
}
```

## Service Lifecycle

```
Bootstrap Scene Load:
├── GlobalPersistentService registered → marked DontDestroyOnLoad
├── SceneTransitionService registered → marked DontDestroyOnLoad
└── BootstrapManager waits for services, then loads Menu

Menu Scene Load:
├── SceneLocalService registered (new for this scene)
├── MenuSceneController injected with:
│   ├── IGlobalService (existing from bootstrap)
│   └── ISceneService (new instance)
└── User clicks "Play"

Scene Transition (Menu → Gameplay):
├── MenuSceneController.OnDestroy() called
├── Menu's SceneLocalService.OnDestroy() → unregisters ISceneService
└── Menu scene unloaded

Gameplay Scene Load:
├── SceneLocalService registered (NEW instance)
├── GameplaySceneController injected with:
│   ├── IGlobalService (SAME instance, persisted)
│   └── ISceneService (NEW instance)
└── Game running...
```

## Scripts Included

| Script | Purpose |
|--------|---------|
| `IGlobalService.cs` | Interface for persistent services |
| `ISceneService.cs` | Interface for scene-local services |
| `ISceneTransitionService.cs` | Interface for scene loading |
| `GlobalPersistentService.cs` | Persists across scenes |
| `SceneLocalService.cs` | Destroyed with scene |
| `SceneTransitionService.cs` | Manages scene loading |
| `BootstrapManager.cs` | Initializes app |
| `MenuSceneController.cs` | Menu logic |
| `GameplaySceneController.cs` | Gameplay logic |

## Build Settings

Add scenes in this order:
1. BootstrapScene (index 0)
2. MenuScene
3. GameplayScene

## Common Patterns

### Scene-Specific Managers
```csharp
// Level manager - exists only in gameplay scene
[Service(typeof(ILevelManager))]
public class LevelManager : ServiceKitBehaviour, ILevelManager
{
    // Automatically unregistered when gameplay scene unloads
}
```

### Checking Service Availability
```csharp
// Scene service might not exist in all scenes
[InjectService(Required = false)]
private ISceneService _sceneService;

void Start()
{
    if (_sceneService != null)
    {
        Debug.Log($"In scene: {_sceneService.SceneName}");
    }
}
```

### Additive Scene Loading
```csharp
// Load UI scene additively
SceneManager.LoadSceneAsync("UIScene", LoadSceneMode.Additive);
// Services from both scenes are available
```

## Important Notes

1. **ServiceKitLocator**: Must be accessible from all scenes
   - Option A: Reference via singleton pattern
   - Option B: Keep in DontDestroyOnLoad object
   - Option C: Use Addressables for the asset

2. **Order Matters**: Global services must initialize before scene services that depend on them

3. **Cleanup is Automatic**: ServiceKitBehaviour.OnDestroy() unregisters services

## Next Steps

- **Sample 9**: Complete game example combining all concepts
