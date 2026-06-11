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
| `AttachedTo.cs` | child-side link — parent + socket (local offsets live in `EntityTransform`) |
| `AssemblyEvents.cs` | `ModuleAttached` / `ModuleDetached` event structs |
| `AssemblyService.cs` | `Attach` / `Detach` / `SetLocalOffset` (writes `EntityTransform`) / queries (immediate or ECB-deferred) |
| `AssemblyExtensions.cs` | `world.Assemblies()`, `module.AttachTo(...)`, `module.DetachFromSocket()`, `parent.TryGetModule(...)` |
| `AssemblyViewBinder.cs` | resolves views, reparents a module under its socket anchor, seeds TRS from `EntityTransform` |
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

scope.DetachFromSocket();                              // or Destroy(rifle) cascades to modules
```

Attaching also sets the module's parent in the core's native hierarchy
(`World.Hierarchy`), and the local offset is stored in the module's
`EntityTransform` — the same component plain (non-socket) parent-child links use.

Save/load is handled by the core `WorldSerializer` — `EntityAssembly`, `Module`,
`AttachedTo` and `EntityTransform` are ordinary serializable components, so a saved
assembly is restored with all its modules and their offsets, and the view hierarchy
is rebuilt by `AssemblyViewBindSystem`. See the design doc §5–§6 for the timing
details (offsets have since moved from `AttachedTo` to `EntityTransform`).

## Phase 2 (authoring) — done

- **Default modules.** `EntityPreset` has a **Default Modules** list
  (`DefaultModule { SocketId, Module }`); they spawn + attach on a fresh
  `Instantiate` only (never on load), so loaded assemblies keep their saved
  modules. See `DefaultModule.cs` / `AssemblyDefaults`.
- **Kind picker.** Author kinds in a `ModuleKindCatalog` asset (Assets ▸ Create ▸
  Sackrany ▸ EOS ▸ Module Kind Catalog); `[ModuleKindField]` on `Module.Kind` /
  `Socket.Kind` shows a dropdown with free-text fallback.
- **Preset inspector** reads the prefab's `SocketSet` to offer socket-id dropdowns
  and a read-only socket list.

## Not yet (Phase 3)

- Entity/view pooling.
- Note: kind validation against a socket happens at view-bind time (when the
  prefab's `SocketSet` is known), not at the `Attach` call — a mismatch logs and
  rolls back.
