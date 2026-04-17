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
