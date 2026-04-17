# Sample 4: Multi-Type Registration

This sample demonstrates registering a single service instance under multiple interface types.

## What You'll Learn

- Using `[Service(typeof(IFoo), typeof(IBar), typeof(IBaz))]`
- Single instance accessible through multiple interfaces
- Fluent API `.As<T>().As<U>()` chaining
- Interface segregation pattern with services

## Setup Instructions

1. **Create a ServiceKitLocator asset** (if not already created)

2. **Set up the scene hierarchy:**
   ```
   Scene
   ├── UnifiedAudioManager (with UnifiedAudioManager component)
   ├── AudioConsumerDemo (with AudioConsumerDemo component)
   ├── MenuMusicController (with MenuMusicController component)
   └── WeaponSfxController (with WeaponSfxController component)
   ```

3. **Assign the ServiceKitLocator** to each ServiceKitBehaviour component

4. **Play the scene** to see multi-type registration in action

## Key Concepts

### Attribute-Based Multi-Type Registration
```csharp
// Single service registered as three different types
[Service(typeof(IAudioPlayer), typeof(IMusicPlayer), typeof(ISoundEffects))]
public class UnifiedAudioManager : ServiceKitBehaviour,
    IAudioPlayer, IMusicPlayer, ISoundEffects
{
    // Implementation of all interfaces...
}
```

### Fluent API Multi-Type Registration
```csharp
var audioManager = new UnifiedAudioManager();

_serviceKitLocator.Register(audioManager)
    .As<IAudioPlayer>()
    .As<IMusicPlayer>()
    .As<ISoundEffects>()
    .Ready();
```

### Consuming Through Different Interfaces
```csharp
// All three inject the SAME instance
[InjectService] private IAudioPlayer _audioPlayer;
[InjectService] private IMusicPlayer _musicPlayer;
[InjectService] private ISoundEffects _soundEffects;

void Demo()
{
    // true - same object, different interface views
    Debug.Log(ReferenceEquals(_audioPlayer, _musicPlayer));
}
```

### Interface Segregation Benefits
```csharp
// MenuMusicController only knows about music
public class MenuMusicController : ServiceKitBehaviour
{
    [InjectService] private IMusicPlayer _musicPlayer;
    // Can only call music methods - clean separation
}

// WeaponController only knows about SFX
public class WeaponController : ServiceKitBehaviour
{
    [InjectService] private ISoundEffects _soundEffects;
    // Can only call SFX methods - focused dependency
}
```

## Scripts Included

| Script | Purpose |
|--------|---------|
| `IAudioPlayer.cs` | General audio control interface |
| `IMusicPlayer.cs` | Music-specific playback interface |
| `ISoundEffects.cs` | Sound effects interface |
| `UnifiedAudioManager.cs` | Single implementation of all three interfaces |
| `AudioConsumers.cs` | Multiple consumers using different interfaces |

## Why Multi-Type Registration?

### 1. Interface Segregation
Components only depend on what they need:
- Settings UI → `IAudioPlayer` (just master volume)
- Menu system → `IMusicPlayer` (background music)
- Game objects → `ISoundEffects` (action sounds)

### 2. Single Source of Truth
One audio manager handles all audio, ensuring:
- Consistent volume mixing
- Proper audio channel management
- Centralized audio settings

### 3. Testability
Each consumer can be tested with a mock of just its interface:
```csharp
// Easy to mock in tests
var mockMusicPlayer = Substitute.For<IMusicPlayer>();
menuController.SetMusicPlayer(mockMusicPlayer);
```

## Common Use Cases

| Service | Interfaces |
|---------|------------|
| Audio Manager | `IAudioPlayer`, `IMusicPlayer`, `ISoundEffects` |
| Network Manager | `INetworkClient`, `INetworkServer`, `INetworkStats` |
| Save System | `ISaveService`, `ILoadService`, `ISaveSlotManager` |
| Input System | `IInputReader`, `IInputRebinder`, `IInputDeviceManager` |

## Next Steps

- **Sample 5**: Handle optional dependencies
- **Sample 6**: Use tags for service filtering
- **Sample 7**: Async service resolution
