# Sample 7: Async Resolution

This sample demonstrates asynchronous service resolution with timeout and cancellation support.

## What You'll Learn

- Using `GetServiceAsync<T>()` to wait for services
- Configuring timeouts with `WithTimeout()`
- Cancellation with `WithCancellation()` and `destroyCancellationToken`
- Error handling with `WithErrorHandling()`
- Async service initialization with `InitializeServiceAsync()`

## Setup Instructions

1. **Create a ServiceKitLocator asset** (if not already created)

2. **Set up the scene hierarchy:**
   ```
   Scene
   ├── NetworkService (with NetworkService component)
   ├── AuthService (with AuthService component)
   ├── CloudSaveService (with CloudSaveService component)
   ├── AsyncConsumerDemo (with AsyncConsumerDemo component)
   └── TimeoutDemo (with TimeoutDemo component)
   ```

3. **Assign the ServiceKitLocator** to all components

4. **Play the scene** to see async resolution in action

## Key Concepts

### Async Service Resolution
```csharp
// Wait for a service to become ready
var networkService = await _serviceKitLocator.GetServiceAsync<INetworkService>();
```

### With Cancellation Token
```csharp
// Cancel resolution when GameObject is destroyed
var service = await _serviceKitLocator.GetServiceAsync<IMyService>(destroyCancellationToken);

// Or use custom cancellation
using var cts = new CancellationTokenSource();
var service = await _serviceKitLocator.GetServiceAsync<IMyService>(cts.Token);
```

### With Timeout
```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
try
{
    var service = await _serviceKitLocator.GetServiceAsync<IMyService>(cts.Token);
}
catch (OperationCanceledException)
{
    Debug.LogWarning("Service resolution timed out");
}
```

### InjectServicesAsync Configuration
```csharp
await _serviceKitLocator.InjectServicesAsync(target)
    .WithTimeout(5f)                                    // 5 second timeout
    .WithCancellation(destroyCancellationToken)         // Cancel on destroy
    .WithErrorHandling(ex => Debug.LogError(ex))        // Handle errors
    .ExecuteAsync();
```

### Async Service Initialization
```csharp
[Service(typeof(INetworkService))]
public class NetworkService : ServiceBehaviour, INetworkService
{
    // Called BEFORE service is marked ready
    protected override async Task InitializeServiceAsync()
    {
        await ConnectToServerAsync();
        // Service becomes ready only after this completes
    }
}
```

## Dependency Chain Timing

```
Timeline:
0.0s - Scene starts
0.0s - NetworkService: Starts connecting...
1.5s - NetworkService: Connected! Now READY
1.5s - AuthService: Dependencies injected, now READY
2.0s - CloudSaveService: Finished init, now READY
2.0s - All services ready!
```

Services automatically wait for dependencies:
- AuthService waits for NetworkService
- CloudSaveService waits for both NetworkService and AuthService

## Error Handling

```csharp
protected override void HandleDependencyInjectionFailure(Exception exception)
{
    if (exception is TimeoutException)
    {
        // Handle timeout - service took too long
        ShowOfflineMode();
    }
    else if (exception is OperationCanceledException)
    {
        // Handle cancellation - usually during shutdown
        // No action needed
    }
    else
    {
        // Handle other errors
        ShowErrorScreen(exception.Message);
    }
}
```

## Scripts Included

| Script | Purpose |
|--------|---------|
| `INetworkService.cs` | Network service interface |
| `IAuthService.cs` | Authentication service interface |
| `ICloudSaveService.cs` | Cloud save service interface |
| `NetworkService.cs` | Slow-initializing network service |
| `AuthService.cs` | Auth service depending on network |
| `CloudSaveService.cs` | Cloud save with multiple dependencies |
| `AsyncConsumerDemo.cs` | Demonstrates async resolution patterns |
| `TimeoutDemo.cs` | Demonstrates timeout handling |

## When to Use Async Resolution

| Scenario | Approach |
|----------|----------|
| ServiceBehaviour dependency | Use `[InjectService]` (automatic) |
| Non-ServiceBehaviour needing services | Use `InjectServicesAsync()` |
| Manual service access after init | Use `GetService<T>()` (sync) |
| Waiting for slow services | Use `GetServiceAsync<T>()` |
| UI loading screens | Use `GetServiceAsync<T>()` with progress |

## Best Practices

1. **Use destroyCancellationToken**: Prevents errors during scene cleanup
2. **Handle TimeoutException**: Don't leave users waiting forever
3. **Keep InitializeServiceAsync fast**: Long init blocks other services
4. **Use WithErrorHandling**: Graceful degradation is better than crashes

## Next Steps

- **Sample 8**: Scene management and service lifecycle
- **Sample 9**: Complete game example combining all concepts
