## [Unreleased]

### Changed
- **Injection failure messages and routing**: failed injections now log with the target object as the Unity log context, so clicking the console entry selects the offending GameObject instead of jumping into ServiceKit's source. Messages name the cause accurately (genuine timeout vs. an awaited service being unregistered vs. the target being destroyed) and include the GameObject's hierarchy path and scene.
- **Quieter scene transitions**: when an injection is cancelled because the target was destroyed (e.g. skipping scenes) the failure is no longer logged at all; when only an awaited service is unregistered while the target survives it is logged as a warning rather than an error. Genuine timeouts, missing required services, and circular dependencies remain errors. Cancellation/timeout failures now throw `ServiceInjectionTimeoutException` (a `TimeoutException` subclass carrying a `ServiceInjectionFailureKind`), so existing `catch (TimeoutException)` continues to work.

## [2.2.0] - 2026-05-31

### Fixed
- **Injection error handling**: `WithErrorHandling` is now honored on the awaited `ExecuteAsync` path (and the `InjectAsync` extension). Previously the handler was ignored there, so `ServiceKitBehaviour` injection failures surfaced as unhandled async-void exceptions and a service could still be marked ready with unsatisfied dependencies.
- **Per-locator dependency graph**: circular-dependency detection, exemptions, and errors are tracked per `ServiceKitLocator` instead of in shared static state, so multiple locators no longer cross-contaminate and one locator's `ClearServices` no longer wipes another's graph.
- **Timeout manager**: injection timeouts use unscaled time, so they still fire when `Time.timeScale` is 0; the editor focus/pause handlers no longer permanently disable the timeout manager after the editor loses focus.
- **Awaiter cancellation**: pending awaiters are completed and cancelled outside the locator lock (and use `RunContinuationsAsynchronously`), so awaiter continuations no longer run synchronously while the lock is held.
- **Scene cleanup in player builds**: scene-to-service matching uses runtime metadata instead of editor-only data, so automatic scene cleanup works in builds, not only in the editor.
- **Multi-type registration**: `Register(...).As<A>().As<B>().Ready()` registers all types before readying any, so a consumer woken by one type no longer observes the others as not-yet-registered.

### Changed
- `DebugLogging` now defaults to `false` for new settings instances (quieter out of the box).
- `RegisterServiceAsync` returns `UniTask` when UniTask is enabled.
- Editor auto-assignment processing is debounced to avoid redundant project scans on bursts of project/hierarchy changes.
- Removed the unused internal `CheckWaitingServices` placeholder and the editor-only static circular-dependency helpers on `ServiceInjectionBuilder` (use `ServiceKitLocator.IsServiceCircularDependencyExempt` / `HasCircularDependencyError` instead).
- Tightened the README thread-safety wording to reflect the lock-guarded registry and main-thread assumptions.

## [2.1.0] - 2026-04-19

### Added
- **Sample 10: Unit Testing** — Demonstrates UseLocator() for mock injection, TestAwake pattern, NSubstitute mock setup, and integration testing with real ServiceKitLocator
- **Sample 11: Error Handling** — Demonstrates TryResolveService/ServiceResolutionStatus, timeout recovery, custom error handlers, and circular dependency exemption with CircularDependencyExempt attribute

### Changed
- **Sample 1**: Added comment explaining why plain C# services use fluent API instead of [Service] attribute
- **Sample 7**: Updated to show InjectAsync one-liner as the primary injection pattern
- **Sample 9**: SaveService now uses tag-based discovery (`GetServicesWithTag("saveable")`) instead of hardcoded service references. Added ISaveable interface. PlayerService implements ISaveable and registers with "saveable" tag.

## [2.0.2] - 2026-04-18

### Changed
- **SEO improvements**: Updated package description, keywords, GitHub topics, and README front matter for discoverability
- Added Unity and MIT license badges to README

## [2.0.1] - 2026-04-18

### Changed
- **Samples directory**: Renamed `Samples/` to `Samples~/` following Unity package convention. Samples are now hidden from the project and importable via Package Manager.

## [2.0.0] - 2026-04-17

### Breaking Changes
- **Attribute-Based Registration**: Replaced `ServiceKitBehaviour<T>` with non-generic `ServiceKitBehaviour` and `[Service]` attribute
  - Services now use `[Service(typeof(IFoo))]` attribute instead of generic inheritance
  - Enables multi-type registration: `[Service(typeof(IFoo), typeof(IBar))]`
  - Concrete type fallback when no attribute provided
  - Eliminates generic type parameter noise from class declarations and inheritance chains

- **`InjectServicesAsync` deprecated**: Renamed to `Inject()`. The old name still works but produces a compiler warning.

- **API Renames** (from v1.x):
  - `Registered` → `IsServiceRegistered`
  - `Ready` → `IsServiceReady`
  - `RegisterService()` → `RegisterServiceWithLocator()`
  - `UnregisterService()` → `UnregisterServiceFromLocator()`
  - `MarkServiceReady()` → `MarkServiceAsReady()`
  - `OnServiceInjectionFailed()` → `HandleDependencyInjectionFailure()`

### Added
- **Fluent Registration API**: Chainable API for service registration
  - `Register(service).As<IFoo>().As<IBar>().WithTags("core").Ready()`
  - Supports multi-type registration, tags, and circular dependency exemption

- **`InjectAsync` extension method**: One-liner for dependency injection
  - `await locator.InjectAsync(this, destroyCancellationToken);`
  - Applies default timeout, cancellation, and error handling automatically

- **`Inject()` builder alias**: Shorter entry point for the fluent injection builder
  - `await locator.Inject(this).WithTimeout(10f).ExecuteAsync();`

- **`TryResolveService` atomic method**: Race-condition-free 3-state service check
  - Returns `ServiceResolutionStatus` enum: `Ready`, `RegisteredNotReady`, or `NotRegistered`
  - Single lock operation replaces the two-call `TryGetService` + `IsServiceRegistered` pattern

- **`[Service]` attribute**: Declarative service type registration
  - Supports multiple interface types per service
  - `CircularDependencyExempt` property for opting out of circular detection
  - Example: `[Service(typeof(IFoo), typeof(IBar), CircularDependencyExempt = true)]`

- **`ServiceKitBehaviour` non-generic base class**: For MonoBehaviour services
  - Reads `[Service]` attribute via reflection (cached for performance)
  - Registers instance under all declared types
  - Full lifecycle: Awake → Register → Inject → Init → Ready
  - `UseLocator()` method for unit testing with mocks

- **Non-Generic Registration Methods**: Added to `IServiceKitLocator`
  - `RegisterService(Type serviceType, object service, ...)`
  - `RegisterServiceWithCircularExemption(Type serviceType, object service, ...)`

- **Intelligent 3-State Optional Dependencies**: `[InjectService(Required = false)]`
  - Service ready → inject immediately
  - Service registered but not ready → wait for it
  - Service not registered → skip injection (field remains null)

- **Service Tags**: Organize and filter services at runtime
  - `AddTagsToService`, `RemoveTagsFromService`, `GetServiceTags`
  - `GetServicesWithTag`, `GetServicesWithAnyTag`, `GetServicesWithAllTags`
  - Tags survive register-to-ready transitions

- **UniTask Integration**: Automatic optimization when UniTask is available
  - Zero-allocation async operations
  - Conditional compilation via `SERVICEKIT_UNITASK` define

- **Memory Performance Optimizations**:
  - `ServiceKitObjectPool` for object pooling of Lists and StringBuilders
  - Zero-allocation service resolution for cached services
  - Eliminated LINQ allocations in hot paths

- **Roslyn Analyzers** (separate package):
  - SK001: `[InjectService]` field should be an interface type
  - SK002: `[InjectService]` field should be private, non-static, non-readonly
  - SK003: `[Service(typeof(IFoo))]` on a class that doesn't implement `IFoo`
  - SK004: Injection chain must include cancellation token
  - SK005: `ServiceKitBehaviour` subclass overrides `Awake()` without calling `base.Awake()`
  - SK010: Prefer `ExecuteWithCancellationAsync` over `WithCancellation().ExecuteAsync()`

- **Comprehensive Test Suite**: 35+ tests covering race conditions, optional dependencies, tags, attribute reflection, multi-interface registration, and stress testing

- **ServiceKit Debug Window**: Enhanced editor window
  - Real-time service monitoring with readiness status
  - Scene-based grouping with DontDestroyOnLoad separation
  - Tag visualization and search/filtering
  - Script navigation and GameObject pinging

### Fixed
- **GetServiceAsync race condition**: Task forwarding now set up inside lock
- **Optional dependency race condition**: Atomic `TryResolveService` replaces non-atomic two-call check
- **UseLocator double-registration**: `Interlocked.CompareExchange` guard prevents concurrent registration
- **Circular dependency string matching**: Uses `Type` references instead of string name comparison
- **DontDestroyOnLoad detection**: Requires both scene name match and `buildIndex == -1`
- **Stack trace parsing**: Scans by namespace instead of hardcoded frame index
- **ObjectPool locking**: Consistent locking across all pool types
- **ServiceKitTimeoutManager**: Proper cleanup on Play Mode exit and application quit
- **TOCTOU race condition**: Atomic `TryGetService` replaces separate check-then-get
- **Awake order race condition**: One-frame delay for optional dependencies allows all services to register

### Migration Guide
See the README.md for detailed migration instructions from v1.x to v2.0.

### Note on Versioning
Releases have been renumbered for clarity. What was previously tagged as various 1.x and 2.x releases during development has been consolidated into two clean releases: **1.0.0** (the stable generic `ServiceKitBehaviour<T>` API) and **2.0.0** (the attribute-based `[Service]` API). All pre-1.0 tags remain in git history for reference.

---

## [1.0.0] - 2025-11-17

The first stable release of ServiceKit, featuring:

- **`ServiceKitBehaviour<T>`** generic base class for MonoBehaviour services
- **Two-phase lifecycle**: Register → Ready with async dependency injection
- **`[InjectService]`** attribute for field-based dependency injection
- **Intelligent 3-state optional dependencies**: Ready → inject, registered → wait, absent → skip
- **Circular dependency detection** with path reporting and exemption support
- **Service tags** for runtime organization and filtering
- **UniTask integration** for zero-allocation async when available
- **Fluent injection builder**: `.WithTimeout().WithCancellation().ExecuteAsync()`
- **`UseLocator()`** for unit testing with mock locators
- **ServiceKit Debug Window** with scene-based grouping, search, and service inspection
- **Addressables support** for loading ServiceKitLocator assets on demand
- **Memory-optimized** object pooling for allocations
- **Comprehensive test suite** for race conditions and edge cases
