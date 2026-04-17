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

---

## [1.4.3] - Aug 29, 2025
### Fixed
- **Edit Mode Test Compatibility**: Fixed DontDestroyOnLoad error in ServiceKitTimeoutManager during Edit Mode tests

### Added
- **Test Coverage**: Comprehensive test for InitializeService timing with optional dependencies

## [1.4.2] - Aug 29, 2025
### Fixed
- **Compilation Error**: Fixed CS0246 error in ServiceKitThreading.cs when SERVICEKIT_UNITASK is not defined

## [1.4.1] - Aug 29, 2025
### Improved
- **Code Quality**: Comprehensive coding standards review and refactoring

## [1.4.0] - Aug 29, 2025
### Added
- **Intelligent 3-State Dependency Resolution**: Enhanced `InjectService` attribute with smart behavior when `Required = false`

### Changed
- **BREAKING CHANGE**: Enhanced `Required = false` behavior - now uses intelligent resolution instead of simple ready-check

## [1.3.3] - Aug 12, 2025
- Improved error handling for service registration failures

## [1.3.2] - Aug 12, 2025
- Hotfix: Added ExecuteWithCancellationAsync method to IServiceInjectionBuilder

## [1.3.1] - Aug 12, 2025
- Moved Roslyn Analyzers to separate repository

## [1.3.0] - Aug 12, 2025
- Abstracted functionality out of ServiceInjectionBuilder
- Added convenience method for execute and cancel

## [1.2.2] - Aug 10, 2025
- Updated package path in README

## [1.2.1] - Aug 04, 2025
- Hotfix for race condition in ServiceKitTimeoutManager

## [1.2.0] - Aug 04, 2025
- Added performance tests and documentation

## [1.1.1] - Jul 29, 2025
- Changed UniTask define from INCLUDE_UNITASK to SERVICEKIT_UNITASK

## [1.1.0] - Jul 29, 2025
- Added UniTask support

## [1.0.9] - Jul 28, 2025
- Added support for service tags

## [1.0.8] - Jul 28, 2025
- Updated ServiceKitServicesTab locator selection logic

## [1.0.7] - Jul 28, 2025
- Enhanced ServiceKitLocatorDrawer auto-assignment

## [1.0.6] - Jul 28, 2025
- Enhanced circular dependency detection

## [1.0.5] - Jul 28, 2025
- Circular dependency visualization in ServiceKit Window

## [1.0.4] - Jul 28, 2025
- Added circular dependency icons to ServiceKit Window

## [1.0.3] - Jul 27, 2025
- Added ServiceKitPlayModeHandler to cleanup state on exiting play mode

## [1.0.2] - Jul 27, 2025
- ServiceKit Window now shows registration status

## [1.0.1] - Jul 27, 2025
- Fix for icon pathing when loaded via package manager

## [1.0.0] - Jul 26, 2025
- Added circular dependency detection
- Simplified ServiceKitBehaviour
- Switched to 2-phase injection process

## [0.3.0] - Jul 13, 2025
- ServiceInjectionBuilder now walks inheritance hierarchy for field injection

## [0.2.0] - Jul 10, 2025
- Added ServiceKitTimeoutManager with timescale-aware timeouts

## [0.1.5] - Jul 07, 2025
- ServiceKitBehaviour calls OnRegister in OnDestroy

## [0.1.4] - Jun 26, 2025
- Made OnServicesInjected abstract

## [0.1.3] - Jun 26, 2025
- Moved ServiceKitProjectSettings to Editor folder

## [0.1.2] - Jun 26, 2025
- Removed ScriptableSingleton usage (Editor-only class)
