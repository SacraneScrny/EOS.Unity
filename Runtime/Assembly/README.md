# EOS.UnityAssembly (Phase 1)

Runtime composition of entities from typed, swappable modules, with save/load.
Full design and rationale: [`../../EOS.UnityAssembly.md`](../../EOS.UnityAssembly.md).

This is a self-contained module inside EOS.Unity (no separate asmdef — it compiles
into `Assembly-CSharp` with the core, like the rest of EOS.Unity).

## Files

| File | Role |
|---|---|
| `ModuleKind.cs` | `ModuleKind` value type + name↔id interner (`ModuleKindRegistry`) |
| `Socket.cs` | `Socket` (id + kind + anchor) and `SocketSet` MonoBehaviour (+ gizmos) authored on the view prefab |
| `Module.cs` | `Module` component — advertises a module entity's `ModuleKind` |
| `EntityAssembly.cs` | root component — authoritative `socketId → child` map (+ snapshot data classes) |
| `AttachedTo.cs` | child-side link — parent, socket, and the per-attachment local offset |
| `AssemblyEvents.cs` | `ModuleAttached` / `ModuleDetached` event structs |
| `AssemblyService.cs` | `Attach` / `Detach` / `SetLocalOffset` / queries (immediate or ECB-deferred) |
| `AssemblyExtensions.cs` | `world.Assemblies()`, `module.AttachTo(...)`, `module.Detach()`, `parent.TryGetModule(...)` |
| `AssemblyViewBinder.cs` | resolves views, reparents a module under its socket anchor, applies offset |
| `AssemblyViewBindSystem.cs` | re-applies view parenting once views exist (load / deferred attach) |

## Quick use

```csharp
using EOS.Unity;
using UnityEngine;

// rifle prefab has a SocketSet with a socket { Id="Optics", Kind="Optics" }
// scope preset's components include a Module { Kind = "Optics" }

var rifle = riflePreset.Instantiate();
var scope = scopePreset.Instantiate();

scope.AttachTo(rifle, "Optics");                       // snap to the anchor
world.Assemblies().SetLocalOffset(scope,               // nudge along the rail (saved)
    new Vector3(0, 0, 0.03f), Quaternion.identity);

scope.Detach();                                        // or Destroy(rifle) cascades to modules
```

Save/load is handled by the core `WorldSerializer` — `EntityAssembly`, `Module`,
and `AttachedTo` are ordinary serializable components, so a saved assembly is
restored with all its modules and their offsets, and the view hierarchy is rebuilt
by `AssemblyViewBindSystem`. See the design doc §5–§6 for the timing details.

## Not in Phase 1

- Default modules authored on an assembly preset (Phase 2).
- `ModuleKind` enum/registry dropdown in the inspector (Phase 2; strings work now).
- Entity/view pooling (Phase 3).
- Kind validation against a socket happens at view-bind time (when the prefab's
  `SocketSet` is known), not at the `Attach` call — a mismatch logs and rolls back.
