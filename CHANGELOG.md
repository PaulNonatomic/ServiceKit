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