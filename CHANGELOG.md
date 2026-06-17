## [2.6.1] - 2026-06-08

### Fixed
- **Dependency-graph edges weren't reset on unregister** (regression from 2.6.0's once-per-type edge caching). Re-registering a service type with a different concrete implementation — common across a scene reload — kept the previous type's dependency edges, so circular-dependency detection could miss a real cycle or report a phantom one. Unregister now invalidates the node.
- **`CleanupDestroyedServices` left pending awaiters hanging.** Unlike the other teardown paths, it removed services without faulting their awaiters, so a consumer awaiting a now-destroyed service hung until timeout. It now faults them with `ServiceUnregisteredException`.
- **`ServiceKitBehaviour`'s registration guard is reset on unregister**, so a pooled/reused object re-registers via `UseLocator()` as documented instead of being silently skipped on its second lifecycle.
- **Off-thread injection with a timeout no longer throws a Unity main-thread exception** — injection hops to the Unity thread before creating the timeout manager (which calls `new GameObject` / reads `Time.*`).
- **`WarnOnDestroyedRegistration` now works.** The setting was never read; registering a destroyed `UnityEngine.Object` now logs a warning when it is enabled (default on).

### Changed
- **README accuracy.** Removed the reference to a benchmark suite that does not ship and reframed the indicative timings honestly; softened "zero allocations" to "greatly reduced allocations" (UniTask is allocation-free, ServiceKit's wrapper is not); reworded "race-condition-free optional resolution" to match what the atomic check actually guarantees. Documented the interface facets, the `ServiceKitExtensions` convenience methods, the `ServiceKitSettings` fields, and the `ServiceInjectionFailureKind` values; added a class summary to `ServiceKitLocator`. Removed a leftover editor console-log block.

## [2.6.0] - 2026-06-07

### Added
- The locator interface is now composed from focused facets — `IServiceLocator` (core register / ready / resolve / inject), `IServiceTagRegistry`, `IServiceSceneManager`, and `IServiceDiagnostics`. `IServiceKitLocator` inherits all four, so existing code and implementers are unaffected; new code and alternative locator implementations can depend on just the slice they need.

### Changed
- **Faster steady-state injection.** When every dependency of an object is already ready, injection now resolves and assigns synchronously — skipping the per-field async state machines, the task array + `WhenAll`, and the thread hop. Additionally, a type's dependency-graph edges are built once instead of on every injection, and the required-services and circular-dependency checks no longer allocate on the success path. Meaningfully less per-spawn GC when instantiating injected prefabs at runtime. Behaviour is unchanged: the fast path only triggers when all dependencies are already ready and the injection is on the Unity thread; anything else takes the existing async path.

### Fixed
- Injection-failure handling no longer risks a Unity main-thread exception when an injection was started off the main thread (or resumed there via `ConfigureAwait(false)`). The failure path now re-syncs to the Unity thread before any Unity-API access and reads play-state from a thread-safe snapshot. The non-UniTask edit-mode defer was also aligned with the UniTask path.
- **README accuracy.** Corrected the UniTask install instructions to the official Cysharp source (`...UniTask#2.5.10`) and removed a corrupted link artifact; documented the typed injection exceptions and the 30-second default timeout; corrected the `IServiceInjectionBuilder` API reference (`Execute()` is not an interface member; added the missing members); and fixed the optional-dependency example to show the injection trigger.

## [2.5.1] - 2026-06-07

### Fixed
- **UniTask compile regression**: 2.4.0 dropped the UniTask assembly reference from the runtime asmdef (and the Unit Testing sample), so ServiceKit failed to compile in any project that has UniTask installed. Restored. The reference is by GUID, so it resolves when UniTask is present and is harmlessly ignored when it is not.
- **WebGL requires UniTask**: WebGL has no thread pool, so the `System.Threading.Tasks` injection path cannot resume an awaiter waiting for a not-yet-ready service — the injection silently hangs (only already-ready dependencies resolve). UniTask's player-loop async resumes correctly, so a WebGL target now requires it. ServiceKit logs a startup warning in a WebGL build without UniTask, and the README documents the requirement. Validated on an IL2CPP WebGL player.
- **Edit-mode injection hang under UniTask**: the one-frame "wait for the Awake phase" defer used `UniTask.NextFrame()`, which never resumes without a running player loop (e.g. edit-mode tooling, or `async Task` tests), hanging injection of optional or not-yet-registered dependencies. Edit mode has no Awake phase to wait for, so the defer is skipped there. Run-state is read from a thread-safe snapshot because the continuation can resume off the main thread.

### Added
- Single-threaded async-resume PlayMode tests (a required dependency that becomes ready mid-wait, and an absent optional dependency) that exercise the injection resume path without the thread pool, regression-guarding the WebGL/UniTask behaviour. Both builds run green: non-UniTask EditMode 113 / PlayMode 19, UniTask EditMode 111 (the two `System.Threading.Tasks`-internals fixtures excluded) / PlayMode 19.

## [2.5.0] - 2026-06-06

### Added
- **Editor hint for concrete-only registration**: in the ServiceKit window, a service registered under its concrete type that implements a user-defined interface (which is not itself registered in the locator) now shows an amber `i` badge. The tooltip suggests registering it as the interface via `[Service(typeof(IFoo))]` — catching the common case of a forgotten attribute, or one placed on an abstract base where it has no effect (the attribute is not inherited).
- Validated dependency injection under **IL2CPP + High managed stripping** on a Standalone player (new `StrippingSmokeTests`); the shipped `link.xml` preserves interface-only services so reflection-based injection survives stripping. Full suite: EditMode 113/113, PlayMode (Mono) 16/16, PlayMode (IL2CPP, High stripping) 17/17.

## [2.4.0] - 2026-06-05

### Added
- `TryResolveService`, `HasCircularDependencyError`, and the `ServiceResolutionStatus` enum are now part of `IServiceKitLocator`, and `ServiceKitBehaviour` exposes a `protected virtual ResolveLocator()` hook. Alternative `IServiceKitLocator` implementations now work end-to-end — including optional-dependency injection, which previously silently no-opped against any non-concrete locator.
- A package `link.xml` preserving the `[Service]`/`[InjectService]` attribute types for IL2CPP managed stripping.

### Changed
- Injectable-field and attribute reflection is memoized per type, removing the `GetFields`/LINQ/`GetCustomAttribute` work that previously ran on every injection and registration.

### Removed
- The unused Unity Test Framework reference is no longer a runtime dependency (dropped from the runtime asmdef and `package.json`), so it no longer ships into player/IL2CPP builds.

> `IServiceKitLocator` gained members — custom implementers of the interface (rare) must add them. Intended for a 2.4.0 release.

## [2.3.0] - 2026-06-05

### Changed
- Injection failures now log with the target as the Unity log context — click the console entry to select the offending GameObject — and name the cause (timeout, service unregistered, or target destroyed) with the GameObject's hierarchy path and scene.
- Quieter scene transitions: a destroyed target is silent and an unregistered service (while the target survives) is a warning; timeouts, missing required services, and circular dependencies stay errors.

### Added
- `ServiceUnregisteredException` (`OperationCanceledException` subclass) and `ServiceInjectionTimeoutException` (`TimeoutException` subclass) so an unregistered service is distinguished from a timeout positively rather than inferred. Both subclass their existing base, so existing `catch` clauses keep working.

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
