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