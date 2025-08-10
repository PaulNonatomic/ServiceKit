# ServiceKit Roslyn Analyzers

This sample contains optional Roslyn analyzers for ServiceKit.

## SK001 — Injected member must be an interface

Warns when `[InjectService]` is applied to a field or property whose declared type is not an interface.

**Example:**
```csharp
[InjectService] private PlayerService _bad;  // ⚠ Warning SK001
[InjectService] private IPlayerService _good; // ✅ OK
