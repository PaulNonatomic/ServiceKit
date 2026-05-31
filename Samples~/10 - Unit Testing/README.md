# Sample 10 - Unit Testing

This sample demonstrates how to unit test `ServiceKitBehaviour` services. It covers two approaches:

1. **Mock-based unit tests** using NSubstitute to isolate services from their dependencies
2. **Integration tests** using a real `ServiceKitLocator` to verify end-to-end wiring

Both approaches rely on the `UseLocator()` method and the `TestAwake()` pattern to drive the service lifecycle from test code.

---

## The TestAwake Pattern

### Why Awake() Can't Be Called Directly

Unity calls `Awake()` automatically when you use `AddComponent<T>()`. You cannot call it yourself, and by the time you have a reference to the component, `Awake()` has already run. Since `ServiceKitBehaviour.Awake()` handles registration, injection, and readiness, this creates a problem: **the lifecycle fires before your test can set up a locator**.

### How UseLocator() Solves This

When `ServiceKitBehaviour.Awake()` runs and finds no locator (the serialized field is null), it skips the lifecycle. You then call `UseLocator()` to provide a locator programmatically. This triggers registration automatically. To complete the remaining lifecycle steps (injection, initialization, readiness), you call `TestAwake()`.

### The TestAwake Method

Every testable `ServiceKitBehaviour` should expose a `TestAwake` method that replicates the full lifecycle:

```csharp
#if SERVICEKIT_UNITASK
public async UniTask TestAwake(CancellationToken cancellationToken)
#else
public async Task TestAwake(CancellationToken cancellationToken)
#endif
{
    RegisterServiceWithLocator();

    await Locator.Inject(this)
        .WithCancellation(cancellationToken)
        .WithTimeout()
        .WithErrorHandling(HandleDependencyInjectionFailure)
        .ExecuteAsync();

    await InitializeServiceAsync();
    InitializeService();
    MarkServiceAsReady();
}
```

This calls the same protected methods that `Awake()` would call internally, giving tests full control over the lifecycle.

### The Full Sequence

```
AddComponent<T>()       --> Awake() fires, but no locator => skips lifecycle
behaviour.UseLocator()  --> Provides locator, triggers RegisterServiceWithLocator()
behaviour.TestAwake()   --> Inject() + InitializeService() + MarkServiceAsReady()
```

After `TestAwake()` completes, the service is fully registered, injected, initialized, and ready -- exactly as it would be at runtime.

---

## Section 1: Testing with Mocks (NSubstitute)

Mock-based tests isolate a service from its dependencies. You replace the locator and injection builder with mocks, then verify that the correct lifecycle methods were called.

### Setting Up the Mocks

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Nonatomic.ServiceKit;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

#if SERVICEKIT_UNITASK
using Cysharp.Threading.Tasks;
#endif

[TestFixture]
public class GameControllerMockTests
{
    private IServiceKitLocator _mockLocator;
    private IServiceInjectionBuilder _mockBuilder;
    private GameObject _testGameObject;

    [SetUp]
    public void Setup()
    {
        _mockLocator = Substitute.For<IServiceKitLocator>();
        _mockBuilder = Substitute.For<IServiceInjectionBuilder>();

        // Set up the fluent API chain so each builder method returns itself
        _mockBuilder.WithCancellation(Arg.Any<CancellationToken>()).Returns(_mockBuilder);
        _mockBuilder.WithTimeout(Arg.Any<float>()).Returns(_mockBuilder);
        _mockBuilder.WithTimeout().Returns(_mockBuilder);
        _mockBuilder.WithErrorHandling(Arg.Any<Action<Exception>>()).Returns(_mockBuilder);

#if SERVICEKIT_UNITASK
        _mockBuilder.ExecuteAsync().Returns(UniTask.CompletedTask);
#else
        _mockBuilder.ExecuteAsync().Returns(Task.CompletedTask);
#endif

        // Wire up Inject() to return the mock builder
        _mockLocator.Inject(Arg.Any<object>()).Returns(_mockBuilder);
    }

    [TearDown]
    public void TearDown()
    {
        if (_testGameObject != null)
        {
            Object.DestroyImmediate(_testGameObject);
            _testGameObject = null;
        }
    }
```

### Verifying Registration and Readiness

```csharp
    [Test]
    public async Task GameController_RegistersWithLocator()
    {
        // Arrange
        _testGameObject = new GameObject("GameController");
        var controller = _testGameObject.AddComponent<GameController>();
        controller.UseLocator(_mockLocator);

        // Act
        await controller.TestAwake(CancellationToken.None);

        // Assert -- RegisterService was called with the correct type
        _mockLocator.Received(1).RegisterService(
            typeof(GameController),
            Arg.Any<object>(),
            Arg.Any<string>()
        );
    }

    [Test]
    public async Task GameController_MarksServiceAsReady()
    {
        // Arrange
        _testGameObject = new GameObject("GameController");
        var controller = _testGameObject.AddComponent<GameController>();
        controller.UseLocator(_mockLocator);

        // Act
        await controller.TestAwake(CancellationToken.None);

        // Assert -- ReadyService was called
        _mockLocator.Received(1).ReadyService(typeof(GameController));
    }

    [Test]
    public async Task GameController_InjectIsCalled()
    {
        // Arrange
        _testGameObject = new GameObject("GameController");
        var controller = _testGameObject.AddComponent<GameController>();
        controller.UseLocator(_mockLocator);

        // Act
        await controller.TestAwake(CancellationToken.None);

        // Assert
        _mockLocator.Received(1).Inject(controller);
    }
}
```

Mock tests are fast and do not require real service instances. They are ideal for verifying that a service participates in the lifecycle correctly.

---

## Section 2: Integration Testing with Real ServiceKitLocator

Integration tests use a real `ServiceKitLocator` (created via `ScriptableObject.CreateInstance`) to verify that services register, inject, and resolve correctly with real dependencies.

### Setting Up the Real Locator

```csharp
[TestFixture]
public class GameControllerIntegrationTests
{
    private ServiceKitLocator _locator;
    private GameObject _scoreGameObject;
    private GameObject _leaderboardGameObject;
    private GameObject _controllerGameObject;

    [SetUp]
    public void Setup()
    {
        _locator = ScriptableObject.CreateInstance<ServiceKitLocator>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_locator != null)
        {
            _locator.ClearServices();
            Object.DestroyImmediate(_locator);
            _locator = null;
        }

        DestroyIfNotNull(ref _scoreGameObject);
        DestroyIfNotNull(ref _leaderboardGameObject);
        DestroyIfNotNull(ref _controllerGameObject);
    }

    private void DestroyIfNotNull(ref GameObject go)
    {
        if (go != null)
        {
            Object.DestroyImmediate(go);
            go = null;
        }
    }
```

### Testing Services With Real Dependencies

```csharp
    [Test]
    public async Task GameController_WithRealDependencies_AllServicesReady()
    {
        // Arrange -- stand up the dependency chain
        _scoreGameObject = new GameObject("ScoreService");
        var scoreService = _scoreGameObject.AddComponent<ScoreService>();
        scoreService.UseLocator(_locator);
        await scoreService.TestAwake(CancellationToken.None);

        _leaderboardGameObject = new GameObject("LeaderboardService");
        var leaderboardService = _leaderboardGameObject.AddComponent<LeaderboardService>();
        leaderboardService.UseLocator(_locator);
        await leaderboardService.TestAwake(CancellationToken.None);

        _controllerGameObject = new GameObject("GameController");
        var controller = _controllerGameObject.AddComponent<GameController>();
        controller.UseLocator(_locator);

        // Act
        await controller.TestAwake(CancellationToken.None);

        // Assert -- all three services are registered and ready
        Assert.IsTrue(_locator.IsServiceReady<IScoreService>());
        Assert.IsTrue(_locator.IsServiceReady<ILeaderboardService>());
        Assert.IsTrue(_locator.IsServiceReady<GameController>());
    }

    [Test]
    public async Task GameController_WithRealDependencies_DependenciesAreInjected()
    {
        // Arrange
        _scoreGameObject = new GameObject("ScoreService");
        var scoreService = _scoreGameObject.AddComponent<ScoreService>();
        scoreService.UseLocator(_locator);
        await scoreService.TestAwake(CancellationToken.None);

        _leaderboardGameObject = new GameObject("LeaderboardService");
        var leaderboardService = _leaderboardGameObject.AddComponent<LeaderboardService>();
        leaderboardService.UseLocator(_locator);
        await leaderboardService.TestAwake(CancellationToken.None);

        _controllerGameObject = new GameObject("GameController");
        var controller = _controllerGameObject.AddComponent<GameController>();
        controller.UseLocator(_locator);

        // Act
        await controller.TestAwake(CancellationToken.None);

        // Assert -- dependencies are injected and usable
        Assert.IsNotNull(controller.ScoreService);
        Assert.IsNotNull(controller.LeaderboardService);
        Assert.AreSame(scoreService, controller.ScoreService);
        Assert.AreSame(leaderboardService, controller.LeaderboardService);
    }

    [Test]
    public async Task GameController_WithRealDependencies_GameplayWorks()
    {
        // Arrange -- full stack
        _scoreGameObject = new GameObject("ScoreService");
        var scoreService = _scoreGameObject.AddComponent<ScoreService>();
        scoreService.UseLocator(_locator);
        await scoreService.TestAwake(CancellationToken.None);

        _leaderboardGameObject = new GameObject("LeaderboardService");
        var leaderboardService = _leaderboardGameObject.AddComponent<LeaderboardService>();
        leaderboardService.UseLocator(_locator);
        await leaderboardService.TestAwake(CancellationToken.None);

        _controllerGameObject = new GameObject("GameController");
        var controller = _controllerGameObject.AddComponent<GameController>();
        controller.UseLocator(_locator);
        await controller.TestAwake(CancellationToken.None);

        // Act -- simulate a game session
        controller.StartNewGame("Alice");
        controller.CollectItem(50);
        controller.CollectItem(30);
        controller.EndGame("Alice");

        // Assert
        Assert.AreEqual(80, scoreService.CurrentScore);
        Assert.AreEqual("Alice", leaderboardService.GetTopPlayer());
    }
}
```

Integration tests verify that the full service graph works together. They catch wiring issues that mock tests cannot.

---

## When to Use Each Approach

| Approach | Best For | Trade-offs |
|---|---|---|
| **Mock tests** | Verifying lifecycle participation, testing error handling, testing a service in isolation | Fast, no real dependencies needed. Cannot catch wiring issues. |
| **Integration tests** | Verifying dependency injection, testing service interactions, end-to-end validation | Catches real bugs. Slower, requires standing up the full dependency chain. |

In practice, use both: mock tests for individual service behavior, integration tests for the dependency graph.

---

## Key Points

- `UseLocator()` accepts any `IServiceKitLocator`, including mocks and real instances.
- `ScriptableObject.CreateInstance<ServiceKitLocator>()` creates a real locator for integration tests -- no scene or prefab required.
- Always call `_locator.ClearServices()` and `Object.DestroyImmediate()` in `TearDown` to prevent test pollution.
- The `TestAwake()` method is not part of the ServiceKit API -- it is a pattern you add to your own services to enable testing.
- Register dependencies **before** calling `TestAwake()` on the service that needs them, so injection can resolve immediately.
