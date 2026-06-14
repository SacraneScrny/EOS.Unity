# EOS.UnityAssembly

Runtime composition of entities from typed, swappable modules, with save/load.

A self-contained module inside EOS.Unity (no separate asmdef — it compiles into
`Assembly-CSharp` with the core, like the rest of EOS.Unity).

## Model

A parent entity exposes named **sockets**; a module entity advertises a **kind**;
attaching a module to a socket succeeds only when the kinds match. Attaching also
sets the module's parent in the core's native hierarchy (`World.Hierarchy`), and
the module's local offset is stored in its `EntityTransform` — the same component
plain parent-child links use. Save/load is handled by the core `WorldSerializer`:
`EntityAssembly`, `Module`, `AttachedTo` and `EntityTransform` are ordinary
serializable components, so a saved assembly restores with all its modules and
their offsets, and the view hierarchy is rebuilt by `AssemblyViewBindSystem`.

## Files

| File | Role |
|---|---|
| `ModuleKind.cs` | `ModuleKind` value type + name↔id interner (`ModuleKindRegistry`) |
| `Socket.cs` | `Socket` (id + kind + anchor) and `SocketSet` MonoBehaviour (+ gizmos) authored on the view prefab |
| `Module.cs` | `Module` component — advertises a module entity's `ModuleKind` |
| `EntityAssembly.cs` | root component — authoritative `socketId → child` map |
| `AttachedTo.cs` | child-side link — parent + socket (local offset lives in `EntityTransform`) |
| `AssemblyEvents.cs` | `ModuleAttached` / `ModuleDetached` event structs |
| `AssemblyService.cs` | `Attach` / `Detach` / `SetLocalOffset` (writes `EntityTransform`) / queries (immediate or ECB-deferred) |
| `AssemblyExtensions.cs` | `world.Assemblies()`, `module.AttachTo(...)`, `module.DetachFromSocket()`, `parent.TryGetModule(...)` |
| `AssemblyViewBinder.cs` | resolves views, reparents a module under its socket anchor, seeds TRS from `EntityTransform` |
| `AssemblyViewBindSystem.cs` | re-applies view parenting once views exist (load / deferred attach) |
| `DefaultModule.cs` | `EntityPreset` default-modules list — spawn + attach on a fresh `Instantiate` (never on load) |

## Authoring

- A **module** is an `EntityPreset` whose components include a `Module { Kind }`.
- An **assembly root** is an `EntityPreset` whose incarnation prefab carries a
  `SocketSet`. Its **Default Modules** list (socket id + module preset) spawns and
  attaches automatically on a fresh `Instantiate`.
- Author kinds in a `ModuleKindCatalog` asset (Assets ▸ Create ▸ Sackrany ▸ EOS ▸
  Module Kind Catalog); `[ModuleKindField]` on `Module.Kind` / `Socket.Kind` shows
  a dropdown with free-text fallback.
- Kind validation against a socket happens at view-bind time (when the prefab's
  `SocketSet` is known), not at the `Attach` call — a mismatch logs and rolls back.

## Quick use

```csharp
using EOS.Unity;
using UnityEngine;

var rifle = riflePreset.Instantiate();
var scope = scopePreset.Instantiate();

scope.AttachTo(rifle, "Optics");
world.Assemblies().SetLocalOffset(scope,
    new Vector3(0, 0, 0.03f), Quaternion.identity);

scope.DetachFromSocket();
```
