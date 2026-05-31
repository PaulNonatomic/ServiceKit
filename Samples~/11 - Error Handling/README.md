# Sample 11 - Error Handling

This sample demonstrates four error handling patterns in ServiceKit: atomic service resolution, timeout recovery, custom error handlers, and circular dependency management.

---

## TryResolveService

`TryResolveService` performs an atomic, lock-protected check that returns one of three states:

```csharp
var status = serviceKit.TryResolveService(typeof(IMyService), out var service);
```

| Status | Meaning | Typical Action |
|---|---|---|
| `ServiceResolutionStatus.Ready` | Service is registered and fully initialized. The `out` parameter contains the service instance. | Use the service immediately. |
| `ServiceResolutionStatus.RegisteredNotReady` | Service has been registered but is still initializing (e.g., waiting for dependencies or async init). The `out` parameter is null. | Wait for it with `GetServiceAsync`, or poll later. |
| `ServiceResolutionStatus.NotRegistered` | No service of this type has been registered at all. The `out` parameter is null. | Use a fallback, skip optional behavior, or log a warning. |

### When to use TryResolveService vs TryGetService

- **`TryGetService<T>(out T service)`** returns `true`/`false` and only succeeds when the service is ready. It cannot distinguish between "not registered" and "registered but still initializing".
- **`TryResolveService(Type, out object)`** returns a three-state enum, letting you make informed decisions when a service is still loading.

Use `TryResolveService` when you need to display loading states, implement fallback logic for services that may never be registered, or make decisions during the window between registration and readiness.

```csharp
var status = serviceKit.TryResolveService(typeof(IAnalytics), out var analytics);
switch (status)
{
    case ServiceResolutionStatus.Ready:
        ((IAnalytics)analytics).TrackEvent("app_start");
        break;
    case ServiceResolutionStatus.RegisteredNotReady:
        // Analytics is loading -- queue the event for later
        _pendingEvents.Add("app_start");
        break;
    case ServiceResolutionStatus.NotRegistered:
        // Analytics module not included in this build -- skip
        break;
}
```

---

## Timeout Handling

### With GetServiceAsync

Pass a `CancellationToken` with a timeout to limit how long you wait for a service:

```csharp
try
{
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
    var service = await serviceKit.GetServiceAsync<ISlowService>(cts.Token);
    // Service resolved within timeout
}
catch (OperationCanceledException)
{
    // Timeout expired -- use fallback behavior
    Debug.LogWarning("Service unavailable within timeout");
}
```

This is useful when you want to proceed with degraded functionality rather than waiting indefinitely.

### With the Inject builder

The `Inject()` builder has a `WithTimeout()` method:

```csharp
await serviceKit.Inject(this)
    .WithCancellation(destroyCancellationToken)
    .WithTimeout(5f)  // 5 second timeout
    .WithErrorHandling(HandleError)
    .ExecuteAsync();
```

Calling `WithTimeout()` without arguments uses the default timeout from ServiceKit settings. Calling `WithTimeout(seconds)` sets a specific duration.

### Recovery strategies

When a timeout occurs, common strategies include:

1. **Fallback values** -- use sensible defaults when a service is unavailable
2. **Retry with backoff** -- attempt resolution again with a longer timeout
3. **Graceful degradation** -- disable features that depend on the timed-out service
4. **User notification** -- inform the player that a feature is loading

---

## Custom Error Handlers

### The HandleDependencyInjectionFailure override

`ServiceKitBehaviour` provides a virtual method that is called when injection fails:

```csharp
[Service(typeof(IMyService))]
public class MyService : ServiceKitBehaviour, IMyService
{
    protected override void HandleDependencyInjectionFailure(Exception exception)
    {
        // Default implementation logs the error.
        // Override to add recovery logic, fallback initialization, etc.
        Debug.LogError($"[MyService] Failed: {exception.Message}");
    }
}
```

This is the simplest approach for services that extend `ServiceKitBehaviour`. The base class automatically passes this handler to the injection builder during `Awake()`.

### The builder pattern (WithErrorHandling)

For non-service MonoBehaviours or manual injection, use the builder pattern:

```csharp
await serviceKit.Inject(this)
    .WithCancellation(destroyCancellationToken)
    .WithTimeout(2f)
    .WithErrorHandling(ex =>
    {
        if (ex is TimeoutException)
            Debug.LogWarning("Injection timed out");
        else
            Debug.LogError($"Injection failed: {ex.Message}");
    })
    .ExecuteAsync();
```

The full builder API:

| Method | Purpose |
|---|---|
| `WithCancellation(token)` | Links a CancellationToken (typically `destroyCancellationToken`) |
| `WithTimeout()` | Uses the default timeout from ServiceKit settings |
| `WithTimeout(float)` | Sets a specific timeout in seconds |
| `WithErrorHandling(Action<Exception>)` | Provides a custom error callback |
| `WithErrorHandling()` | Uses the default error handler (logs the error) |
| `ExecuteAsync()` | Runs the injection and returns a Task/UniTask |
| `ExecuteWithCancellationAsync(token)` | Shorthand for `WithCancellation(token).ExecuteAsync()` |

---

## Circular Dependencies

### What causes them

A circular dependency occurs when two or more services depend on each other:

```
ServiceA --[InjectService]--> ICircularB (implemented by ServiceB)
ServiceB --[InjectService]--> ICircularA (implemented by ServiceA)
```

Neither service can complete injection because each is waiting for the other to become ready.

### How ServiceKit detects them

ServiceKit builds a dependency graph at injection time. When `Inject()` is called on a service, ServiceKit registers the service type as "currently resolving" and records its dependencies. If a dependency is found that is already in the "resolving" set, a circular dependency is detected and an exception is thrown.

### How to use CircularDependencyExempt

To break the cycle, mark one side of the dependency with `CircularDependencyExempt = true` on the `[Service]` attribute:

```csharp
[Service(typeof(ICircularB), CircularDependencyExempt = true)]
public class CircularServiceB : ServiceKitBehaviour, ICircularB
{
    [InjectService] private ICircularA _circularA;
    // ...
}
```

When a service is marked as exempt:

1. It is skipped during circular dependency detection
2. It is still registered and injected normally
3. Its dependencies are still resolved -- only the cycle check is bypassed
4. You can query the exemption status at runtime:

```csharp
bool isExempt = serviceKit.IsServiceCircularDependencyExempt<ICircularB>();
```

### Guidelines

- Only exempt **one side** of a circular dependency. Exempting both sides disables detection entirely for that pair.
- Prefer restructuring your dependencies to eliminate cycles when possible. Common approaches:
  - Extract a shared interface or event bus
  - Use lazy initialization or callbacks instead of direct injection
  - Split one service into two (one for data, one for behavior)
- Use `CircularDependencyExempt` as a pragmatic escape hatch when restructuring is not practical.

---

## Scene Setup

To run this sample:

1. Create a `ServiceKitLocator` ScriptableObject asset (right-click in Project > Create > ServiceKit > ServiceKitLocator)
2. Create GameObjects for each service: `ConfigService`, `SlowService`, `CircularServiceA`, `CircularServiceB`
3. Add the corresponding component to each GameObject and assign the ServiceKitLocator reference
4. Create a GameObject for `ErrorHandlingDemo`, add the component, and assign the same ServiceKitLocator reference
5. Enter Play mode and observe the Console for output from each demo section
