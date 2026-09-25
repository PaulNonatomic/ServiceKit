# Addressable Locator

Shows how to load a `ServiceKitLocator` through **Addressables** instead of a direct serialized
reference, using `AddressableServiceKitBehaviour`.

## Why

A direct `[SerializeField] ServiceKitLocator` reference ties the locator asset into every bundle or
scene that references it. Loading it by Addressables reference keeps it a single, shared, ref-counted
asset and gives you explicit load/release. `AddressableServiceKitBehaviour` does the async load before
the service registers, and releases the handle on destroy - you don't write any of that boilerplate.

> Requires the **Addressables** package (`com.unity.addressables`). The sample code is compiled only
> when it is installed (the `SERVICEKIT_ADDRESSABLES` define).

## Setup

1. Create a `ServiceKitLocator` asset: **Assets > Create > ServiceKit > ServiceKitLocator**.
2. Mark it **Addressable** (tick the *Addressable* box in its inspector, or add it to an Addressable
   group).
3. Put `ExampleAddressableService` on a GameObject. In its inspector:
   - assign the locator asset to the **`Service Kit Locator Reference`** (the AssetReference field);
   - leave the inherited **`Service Kit Locator`** field **empty**.
4. Press Play. On `Awake` the service loads the locator via Addressables, registers itself, and runs
   `InitializeService()`. On destroy, the Addressables handle is released.

## Notes

- This pattern fits a **service that registers itself** into an addressably-loaded locator. If instead
  you have a central object that loads the locator once and populates it from another system, that is a
  different shape and this base class won't simplify it - load the locator yourself and use the locator
  API directly.
- The async load uses `UniTask` automatically if it is installed, otherwise `System.Threading.Tasks`.
