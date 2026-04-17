# Sample 6: Service Tags

This sample demonstrates organizing and filtering services using tags for common game scenarios like save/load and new game.

## What You'll Learn

- Using `.WithTags()` during registration
- `GetServicesWithTag()` for single tag filtering
- `GetServicesWithAnyTag()` for OR filtering
- `GetServicesWithAllTags()` for AND filtering
- Real-world use case: Save system and New Game functionality

## Setup Instructions

1. **Create a ServiceKitLocator asset** (if not already created)

2. **Set up the scene hierarchy:**
   ```
   Scene
   └── TagFilteringDemo (with TagFilteringDemo component)
   ```

3. **Assign the ServiceKitLocator** to the TagFilteringDemo component

4. **Play the scene** to see tag filtering in action

## Key Concepts

### Registering with Tags
```csharp
_serviceKitLocator.Register(playerData)
    .As<PlayerDataService>()
    .WithTags("saveable", "resettable", "gameplay")
    .Ready();

_serviceKitLocator.Register(settings)
    .As<SettingsService>()
    .WithTags("saveable", "settings")  // Not resettable!
    .Ready();
```

### Tag Filtering Methods

**Single Tag (exact match):**
```csharp
var saveableServices = _locator.GetServicesWithTag("saveable");
// Returns: PlayerData, Inventory, Settings, Achievements
```

**Any Tag (OR logic):**
```csharp
var services = _locator.GetServicesWithAnyTag("gameplay", "settings");
// Returns: Services with "gameplay" OR "settings" tag
```

**All Tags (AND logic):**
```csharp
var services = _locator.GetServicesWithAllTags("saveable", "resettable");
// Returns: Only services with BOTH tags (PlayerData, Inventory)
```

## Service Tag Matrix

| Service | saveable | resettable | Other Tags |
|---------|:--------:|:----------:|------------|
| PlayerDataService | ✓ | ✓ | gameplay |
| InventoryService | ✓ | ✓ | gameplay |
| SettingsService | ✓ | | settings |
| AchievementService | ✓ | | progression |
| SessionService | | ✓ | runtime |

## Real-World Use Cases

### Save Game
```csharp
public void SaveGame()
{
    var saveData = new Dictionary<string, object>();

    foreach (var info in _locator.GetServicesWithTag("saveable"))
    {
        if (info.Service is ISaveable saveable)
        {
            saveData[saveable.SaveKey] = saveable.GetSaveData();
        }
    }

    // Serialize and write saveData to disk
}
```

### New Game (Reset)
```csharp
public void NewGame()
{
    // Only resets gameplay services, NOT settings or achievements
    foreach (var info in _locator.GetServicesWithTag("resettable"))
    {
        if (info.Service is IResettable resettable)
        {
            resettable.Reset();
        }
    }
}
```

### Find All Core Services
```csharp
var coreServices = _locator.GetServicesWithTag("core");
```

### Find Network-Related Services
```csharp
var networkServices = _locator.GetServicesWithAnyTag("network", "multiplayer", "matchmaking");
```

## Scripts Included

| Script | Purpose |
|--------|---------|
| `ISaveable.cs` | Interface for services that can save state |
| `IResettable.cs` | Interface for services that can be reset |
| `PlayerDataService.cs` | Player data (saveable + resettable) |
| `InventoryService.cs` | Inventory (saveable + resettable) |
| `SettingsService.cs` | Settings (saveable only) |
| `AchievementService.cs` | Achievements (saveable only) |
| `SessionService.cs` | Session data (resettable only) |
| `TagFilteringDemo.cs` | Demonstrates tag filtering |

## Common Tag Patterns

| Tag | Purpose |
|-----|---------|
| `saveable` | Services that persist to save files |
| `resettable` | Services reset on new game |
| `core` | Essential services (always loaded) |
| `gameplay` | In-game systems |
| `ui` | User interface services |
| `network` | Network/multiplayer services |
| `audio` | Sound-related services |
| `analytics` | Tracking/telemetry services |

## Benefits of Tags

1. **Decoupled Systems**: Save system doesn't need to know about every service
2. **Flexible Organization**: Add/remove services without changing consumers
3. **Multiple Categories**: One service can have multiple tags
4. **Runtime Discovery**: Find services dynamically by category

## Next Steps

- **Sample 7**: Async service resolution with timeouts
- **Sample 8**: Scene management and service lifecycle
- **Sample 9**: Complete game example combining all concepts
