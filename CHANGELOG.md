## [2.1.7] - 2025-10-06
- Added an early return to ServiceKitBehaviour.IsServiceLocatorMissing when the application is quitting

## [2.1.6] - 2025-09-15
- Deleted a test with an invalid meta file

## [2.1.5] - 2025-09-10
### Fixed
- **TOCTOU Race Condition in Optional Dependencies**: Fixed critical race condition in optional dependency resolution
  - Previously used separate IsServiceReady() and GetService() calls which weren't atomic
  - Service could be unregistered between the check and get operations (e.g., during scene unload)
  - This caused InitializeService to be called with null optional dependencies despite services being ready
  - Fixed by using atomic TryGetService() operation that checks and gets under a single lock
  - Eliminates race condition where ServiceB's InitializeService could be called with null ServiceA

- **Services Not Injected on Ignored Cancellation**: Fixed critical bug where resolved services were not injected when cancellation was ignored
  - When application quits or ShouldIgnoreCancellation returns true, ExecuteAsync would return early without injecting already-resolved services
  - This caused InitializeService to be called with null dependencies even though services were successfully resolved
  - Fixed by ensuring resolved services are always injected before returning, even when cancellation is ignored

- **Awake Order Race Condition for Optional Dependencies**: Fixed race condition where optional dependencies were incorrectly treated as absent
  - When ServiceA with optional dependency on ServiceB had its Awake() called before ServiceB's Awake(), ServiceB would be null
  - This occurred because Unity's Awake order is non-deterministic within a scene
  - ServiceA would check for ServiceB before ServiceB had a chance to register itself
  - Fixed by adding a one-frame delay when optional dependencies are not registered, allowing all services in the scene to complete their Awake phase
  - Now correctly distinguishes between "not registered yet" and "truly absent" optional dependencies

### Improved
- **Code Quality**: Enhanced code standards and self-documentation
  - Renamed methods for clarity (e.g., WaitForAwakePhaseCompletion)
  - Removed redundant comments and debug logging
  - Extracted complex logic into well-named helper methods
  - Improved overall code maintainability and readability

## [2.1.3] - 2025-09-09
### Fixed
- **Optional Dependency Race Condition**: Fixed critical bug in optional dependency resolution
  - Optional dependencies marked with `Required = false` that were registered but not ready now correctly wait for the service
  - Previously, multiple waiters for the same service could interfere with each other's cancellation tokens
  - Fixed by implementing per-caller TaskCompletionSource to isolate cancellation behavior
  - This ensures the documented 3-state intelligent resolution works correctly:
    - Service ready → inject immediately
    - Service registered but not ready → wait for it (treat as temporarily required)
    - Service not registered → skip injection (field remains null)

### Improved
- **Code Quality**: Enhanced ServiceInjectionBuilder for better maintainability
  - Extracted timeout exception building into well-named helper methods
  - Improved self-documentation with clear method names
  - Added explicit comments explaining optional dependency behavior
  - Removed debug logging and simplified async service resolution

## [2.1.2] - 2025-09-02
## [2.1.1] - 2025-09-02
- Removed performance tests as they had corrupted meta files

## [2.1.0] - 2025-09-02
### Added
- **Memory Performance Optimizations**: Comprehensive memory allocation improvements
  - Added `ServiceKitObjectPool` for object pooling of Lists and StringBuilders
  - Eliminated LINQ allocations in hot paths (GetAllServices, GetServicesWithTag, etc.)
  - Replaced string concatenation with pooled StringBuilder usage
  - Added pre-allocated lists for batch operations in TimeoutManager
  - Zero-allocation service resolution for cached services

- **Memory Performance Tests**: New comprehensive test suite for memory profiling
  - `MemoryAllocationTracker` utility for precise allocation measurement
  - `ServiceKitMemoryPerformanceTests` for core operation allocation testing
  - `LinqVsOptimizedComparisonTests` comparing LINQ vs optimized implementations
  - `ServiceKitMemoryBenchmarkRunner` for automated benchmark execution with CSV export

### Fixed
- **Runtime Exit Errors**: Fixed ServiceKitTimeoutManager cleanup issues
  - Resolved "objects were not cleaned up" warning when exiting Play Mode
  - Fixed timeout exceptions being thrown during application quit
  - Added proper cleanup in OnDestroy, OnApplicationQuit, and editor mode transitions
  - Enhanced ServiceKitPlayModeHandler to properly clean up on exit

### Improved
- **Code Standards**: Enhanced self-documenting code throughout
  - Refactored ServiceKitTimeoutManager with descriptive method names
  - Improved ServiceKitBehaviour with better encapsulation
  - Enhanced ServiceKitObjectPool with generic helper methods
  - Consistent preprocessor directive formatting (no indentation)
  - Better separation of concerns with extracted helper methods

- **Performance**: Significant reduction in runtime allocations
  - GetService<T>: Now allocation-free for cached services
  - IsServiceReady<T>: Zero allocations
  - GetAllServices: Reduced allocations by ~70% through pooling
  - String operations: 90% reduction through StringBuilder pooling

## [2.0.0] - 2025-09-01
### Breaking Changes
- **ServiceKitBehaviour API Changes**: Renamed methods and fields for improved self-documenting code
  - Protected Fields:
    - `Registered` → `IsServiceRegistered`
    - `Ready` → `IsServiceReady`
  - Protected Methods:
    - `RegisterService()` → `RegisterServiceWithLocator()`
    - `UnregisterService()` → `UnregisterServiceFromLocator()`
    - `InjectServicesAsync()` → `InjectDependenciesAsync()`
    - `MarkServiceReady()` → `MarkServiceAsReady()`
    - `OnServiceInjectionFailed()` → `HandleDependencyInjectionFailure()`

### Improved
- **Code Quality**: Major refactoring for self-documenting code
  - All method names now clearly express their intent
  - Improved variable naming throughout the codebase
  - Extracted helper methods for better separation of concerns
  - Enhanced readability following Microsoft C# coding guidelines
  - Simplified cancellation token handling using Unity's built-in `destroyCancellationToken`

### Migration Guide
See the README.md for detailed migration instructions from v1.x to v2.0

## [1.4.3] - Aug 29, 2025
### Fixed
- **Edit Mode Test Compatibility**: Fixed DontDestroyOnLoad error in ServiceKitTimeoutManager during Edit Mode tests
  - Added conditional compilation to only call DontDestroyOnLoad in Play Mode
  - Ensures ServiceKitTimeoutManager works correctly in Edit Mode tests, Play Mode, and built applications

### Added
- **Test Coverage**: Added comprehensive test for InitializeService timing with optional dependencies
  - Verifies that InitializeService waits for all registered dependencies (even optional ones) to become ready
  - Confirms 3-state dependency resolution behavior is working as designed

## [1.4.2] - Aug 29, 2025
### Fixed
- **Compilation Error**: Fixed CS0246 error in ServiceKitThreading.cs when SERVICEKIT_UNITASK is not defined
  - Added conditional using statements to properly import System.Threading.Tasks namespace
  - Ensures Task type is available in both UniTask and standard .NET Task scenarios

## [1.4.1] - Aug 29, 2025
### Improved
- **Code Quality**: Comprehensive coding standards review and refactoring
  - Applied "never nesting" principle - eliminated all nested if statements with early returns and guard clauses
  - Enhanced self-documenting code with clear, action-oriented method names
  - Applied SOLID principles with improved Single Responsibility adherence
  - Better separation of concerns in ServiceInjectionBuilder, ServiceKitLocator, and ServiceKitBehaviour
  - Improved method names and variable naming for better code readability
  - No functional changes - purely internal code quality improvements

## [1.4.0] - Aug 29, 2025
### Added
- **Intelligent 3-State Dependency Resolution**: Enhanced `InjectService` attribute with smart behavior when `Required = false`
  - **Service is ready** → Inject immediately
  - **Service is registered but not ready** → Wait for it (treat as required temporarily)
  - **Service is not registered** → Skip injection (field remains null)
- Comprehensive test coverage for all three dependency resolution states
- Mixed scenario testing for complex dependency graphs

### Changed
- **BREAKING CHANGE**: Enhanced `Required = false` behavior - now uses intelligent resolution instead of simple ready-check
- Updated `InjectServiceAttribute` documentation to reflect new 3-state behavior
- Updated README.md with detailed explanation of intelligent dependency resolution

### Improved
- Eliminates guesswork in optional dependency management
- More predictable behavior for developers
- Better handling of services that are "coming soon" vs "never coming"
- Maintains backward compatibility for `Required = true` (default behavior unchanged)

## [1.3.3] - Aug 12, 2025
- Improved error handling for service registration failures
- Enhanced null service detection to identify missing interface implementations
- Added detailed error messages showing which interfaces a ServiceKitBehaviour failed to implement
- ServiceKitBehaviour now provides proactive error checking before attempting registration

## [1.3.2] - Aug 12, 2025
- Hotfix added the ExecuteWithCancellationAsync method to the IServiceInjectionBuilder

## [1.3.1] - Aug 12, 2025
- hotfix to remove the hidden tools directory. The content has been moved to it's own repo for ServiceKit Roslyn Analyzers
- updated the path to retrieve the Roslyn Analyzers dll to always pull the latest.

## [1.3.0] - Aug 12, 2025
- Abstracted some functionality out of the ServiceInjectionBuilder as it was bloated
- Added a convieniance method to execute and cancel a services injection request.

## [1.2.2] - Aug 10, 2025
- Updated package path in README

## [1.2.1] - Aug 04, 2025
- Hotfix for race condition in ServiceKitTimeoutManager
- Added tests for race condition

## [1.2.0] - Aug 04, 2025
- Added performance tests
- Added documentation on performance

## [1.1.1] - Jul 29, 2025
- Changed the define for Unitask inclusion from INCLUDE_UNITASK to SERVICEKIT_UNITASK

## [1.1.0] - Jul 29, 2025
- Added UniTask support for performance gains out of the box

## [1.0.9] - Jul 28, 2025
- Added support for service tags

## [1.0.8] - Jul 28, 2025
- Updated the ServiceKitServicesTab to use the same logic as ServiceKitLocatorDrawer to select the ServiceKitLocator.

## [1.0.7] - Jul 28, 2025
- Enhanced the ServiceKitLocatorDrawer to assign the ServiceKitLocator without having to look at the component in the inspector.
- Also improved how the instance of ServiceKitLocator is selected. By default it will now select the instance in
  the package unless ServiceKitSettings provide an alternative.

## [1.0.6] - Jul 28, 2025
- Enhanced circular dependency detection to detect circular dependencies created later in the registration process.

## [1.0.5] - Jul 28, 2025
- Services that encounter circular dependencies will be highlighted red in the ServiceKit Window with 
  a warning icon and tooltip explaining the issue.

## [1.0.4] - Jul 28, 2025
- Added an infinite icon to the ServiceItems displayed in the ServiceKit Window. The icon is coloured when circular 
  dependency detection is enabled for the service and greyed out when detection is disabled.
- Added tooltip to the infinite icon to explain the meaning.

## [1.0.3] - Jul 27, 2025
- As the ServiceKitLocator is a ScriptableObject it is capable of persisting state in Editor.
  I've added the ServiceKitPlayModeHandler to cleanup the state of the ServiceKitLocator when exiting play mode in editor.

## [1.0.2] - Jul 27, 2025
- ServiceKit Windowe now shows the registration status of each service

## [1.0.1] - Jul 27, 2025
- Fix for icon pathing issues when package loaded via package manager

## [1.0.0] - Jul 26, 2025
- Added circular dependency detection
- Simplified the use of ServiceKitBehaviour
- Switched to a 2 phase injection process

## [0.3.0] - Jul 13, 2025
- Updated the ServiceInjectionBuilders GetFieldsToInject method which only looks at the specific type passed to it
  but will now walk up the inheritance hierarchy and inject parents
- Added additional tests for this scenario

## [0.2.0] - Jul 10, 2025
- Updated the ServiceInjectionBuilder to introduce the ServiceKitTimeoutManager which provides a revised timeout system
  that respects timescale and provides more insightful error messages.

## [0.1.5] - Jul 07, 2025
- ServiceKitBehaviour now calls OnRegister in OnDestroy 

## [0.1.4] - Jun 26, 2025
- Made the OnServicesInjected method abstract 

## [0.1.3] - Jun 26, 2025
- Moved the ServiceKitProjectSettings into the Editor folder to prevent it being included in builds

## [0.1.2] - Jun 26, 2025
- Removed use of ScriptableSingleton in Settings because it's an Editor only class