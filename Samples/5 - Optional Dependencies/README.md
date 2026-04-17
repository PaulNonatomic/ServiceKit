# Sample 5: Optional Dependencies

This sample demonstrates the intelligent 3-state dependency resolution system for optional services.

## What You'll Learn

- Using `[InjectService(Required = false)]` for optional dependencies
- Understanding the 3-state resolution behavior
- Graceful degradation patterns when services are unavailable
- Common use cases for optional dependencies

## Setup Instructions

### Setup A: All Services (Full Feature Set)
```
Scene
├── CoreService (required - always present)
├── AnalyticsService (optional)
├── AdService (optional)
└── OptionalDependencyDemo
```

### Setup B: Core Only (Minimal/Premium)
```
Scene
├── CoreService (required)
└── OptionalDependencyDemo
```

Try both setups to see how the demo gracefully handles missing optional services!

## Key Concepts

### 3-State Dependency Resolution

When `Required = false`, ServiceKit uses intelligent resolution:

| Service State | Behavior |
|--------------|----------|
| **Ready** | Inject immediately |
| **Registered but not ready** | Wait for it (temporary required) |
| **Not registered** | Skip injection (field remains null) |

### Declaration
```csharp
[Service(typeof(MyConsumer))]
public class MyConsumer : ServiceKitBehaviour
{
    // Required - will fail if not available
    [InjectService]
    private ICoreService _core;

    // Optional - will be null if service doesn't exist
    [InjectService(Required = false)]
    private IAnalyticsService _analytics;

    // Optional - safe to check for null
    [InjectService(Required = false)]
    private IAdService _ads;
}
```

### Graceful Degradation Patterns

**Pattern 1: Null Check**
```csharp
if (_analyticsService != null)
{
    _analyticsService.TrackEvent("game_start");
}
```

**Pattern 2: Null-Conditional Operator**
```csharp
_analyticsService?.TrackEvent("game_start");
```

**Pattern 3: Alternative Behavior**
```csharp
if (_adService != null && _adService.AdsEnabled)
{
    _adService.ShowRewarded(OnRewardComplete);
}
else
{
    // Premium user experience - grant reward directly
    GrantReward();
}
```

## Common Use Cases

| Optional Service | Scenario |
|-----------------|----------|
| **IAnalyticsService** | Disabled in dev builds, enabled in production |
| **IAdService** | Disabled for premium users |
| **ICloudSaveService** | Offline mode fallback to local saves |
| **IAchievementService** | Platform-specific (Steam, PlayStation, etc.) |
| **ILeaderboardService** | Offline mode support |
| **IVibrationService** | Not available on all devices |

## Scripts Included

| Script | Purpose |
|--------|---------|
| `ICoreService.cs` | Required service interface |
| `IAnalyticsService.cs` | Optional analytics interface |
| `IAdService.cs` | Optional advertising interface |
| `CoreService.cs` | Always-available core implementation |
| `AnalyticsService.cs` | Optional analytics implementation |
| `AdService.cs` | Optional ad implementation |
| `OptionalDependencyDemo.cs` | Demonstrates optional dependency handling |

## Why 3-State Resolution?

The 3-state system solves a common problem:

**Without 3-state:**
```
- Service not registered → Injection fails immediately
- Service registered but slow → Timeout waiting
```

**With 3-state:**
```
- Service not registered → Skip gracefully (null)
- Service registered but slow → Wait for it
- Service ready → Inject immediately
```

This means:
1. Truly optional services don't block initialization
2. Slow-but-present services are still awaited
3. You can build features that work with or without certain services

## Testing Configurations

Try these scene configurations:

1. **Full Features**: All services present → Full functionality
2. **No Analytics**: Remove AnalyticsService → Tracking silently skipped
3. **No Ads**: Remove AdService → Rewards granted directly
4. **Core Only**: Only CoreService → Minimal viable product

## Next Steps

- **Sample 6**: Use tags for service filtering
- **Sample 7**: Async service resolution with timeouts
- **Sample 8**: Scene management and service lifecycle
