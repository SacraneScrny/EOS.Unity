# EOS.UnityAssembly — design

Runtime composition of entities from typed, swappable modules, with save/load —
the EOS-native re-imagining of `SackranyPawnAssembly`. **Design document, not yet
implemented.** Decisions locked: a dedicated `ModuleKind` type for socket/module
typing, and view transforms are **reparented** under socket anchors (a real
visual hierarchy, as in the original).

---

## 1. What we are porting

`SackranyPawnAssembly` solves one problem: **assemble a composite object from
typed modules at runtime, then save and restore the whole construction.** Strip
the MonoBehaviour framework and the moving parts are:

| Original piece | Role |
|---|---|
| `Pawn` + `Limb` | entity + its behaviours |
| `PawnAssemblyReference.Guid` | stable design-time id of a prefab, found in `Resources` |
| `PawnAssemblyResourcesCache` | scans `Resources` for parts/assemblies by guid |
| `PawnAssembly` (root) | composite root; runtime guid; serializes the part hierarchy |
| `PawnAssemblyModuleRoot` + `Point[]` | **typed sockets** on transforms (a point accepts one `IAssemblyModuleType`) |
| `IAssemblyModuleType` + `TypeRegistry<T>` | deterministic id per module type |
| `AssemblyDynamicModules` | runtime add/remove, default modules, per-type/per-root tracking, events |
| `PawnAssemblyPool` | pooling by reference guid (Pop/Push/PreWarm) |
| `PawnAssemblyRuntimeRegister` | runtime guid → live instance |
| `PawnAssemblySerializer` | bespoke save format: position/rotation + parts (ref-guid + hierarchy path + local transform + limb data) |

## 2. The key insight

Most of that machinery only existed because the original lived in a bare
MonoBehaviour world. **EOS already provides the equivalents**, so the port is
mostly *deletion by reuse*, with exactly one genuinely new primitive to build.

| Original | Reused EOS / EOS.Unity facility |
|---|---|
| `PawnAssemblyReference.Guid` + `ResourcesCache` | **`IncarnationDatabase` / `incarnations.json`** — id == prefab path, with rename redirects |
| `PawnAssemblyRuntimeRegister` | **EOS stable keys** (`SetStableKey` / `TryFind`) — already serialization-stable |
| `PawnAssemblySerializer` + `PawnAssembliesData` | **`WorldSerializer`** — serializes entities/components/tags and **remaps cross-entity references** (`localId` → `IDeserializeContext.Resolve`) |
| `IAssemblyLimb.OnAssemblyDeserialize` (rewire siblings) | entity references are restored by the serializer; a reactive `[New]` system re-applies *view* parenting |
| authoring parts (`Pawn` prefabs in Resources) | **`EntityPreset` / `EntityComponentSet`** — templates + deep-copy already exist |
| `Pawn` + `Limb` | **`EosEntity` + `EosObject`** |

The **one thing EOS lacks**: a typed, serializable **parent → socket → child
graph between entities**. EOS is a flat ECS — there is no entity hierarchy (the
only `parent/child` in the core is in the system sorter). That graph — plus the
attach/detach API and the view-reparenting that follows it — is all we build.
Everything else is wiring into existing seams.

**Two incidental upgrades over the original, for free:**

- **Save stability.** The original `TypeRegistry` handed out ids by sorted type
  name; add or remove a module type and the ids shift. We never serialize a
  `ModuleKind` id — we serialize its **name** and re-intern on load (the same way
  EOS tags serialize by descriptor). Adding kinds can't corrupt old saves.
- **Nesting is implicit.** A module entity can itself carry an `EntityAssembly`
  with its own sockets. The graph is a tree, not one level, with no extra code —
  the original got nesting through `PawnAssemblyModuleRoot` recursion.

## 3. Placement & naming

- **Location:** `EOS.Unity/Runtime/Assembly/` (+ `EOS.Unity/Editor/Assembly/`).
- **Namespace:** `EOS.Unity` (the package is flat today; no reason to fork it).
- **No separate asmdef.** EOS.Unity deliberately ships none so everything lands
  in `Assembly-CSharp` and sees the asmdef-less core. A standalone
  `EOS.UnityAssembly.asmdef` could not reference `Assembly-CSharp` types, so it
  would not compile. "EOS.UnityAssembly" is therefore a **self-contained module**
  inside EOS.Unity. If the core ever moves into an asmdef, this module gets its
  own asmdef in the same pass.
- **Names stay in the `Entity*` family** (`EntityPreset`, `EntityIncarnation`,
  `EntityComponentSet`). The root component is **`EntityAssembly`** — which also
  dodges the clash with `System.Reflection.Assembly`. No "Pawn" anywhere.

## 4. The model

### 4.1 `ModuleKind` — the socket/module type (locked: dedicated type)

A lightweight typed key, interned to an int for fast compare, serialized by name.

```csharp
public readonly struct ModuleKind : IEquatable<ModuleKind>
{
    public int Id { get; }                 // interned, fast equality
    public string Name { get; }            // stable identity, what we serialize
    public bool IsValid => Id >= 0;

    public static ModuleKind Of(string name);                  // "Barrel"
    public static ModuleKind Of<TEnum>(TEnum v) where TEnum : Enum;  // enum support
    public static readonly ModuleKind None;
}

internal static class ModuleKindRegistry  // name <-> id interner, like TagRegistry
{
    public static int Intern(string name);
    public static string NameOf(int id);
}
```

A socket declares the kind it accepts; a module advertises the kind it is; attach
succeeds only when they match. (This is the `IAssemblyModuleType.Id` check from
`PawnAssemblyModuleRoot.SetModule`, made explicit and save-stable.)

### 4.2 Sockets live on the view (locked: reparent view)

Sockets are inherently spatial — a transform plus an accepted kind — so they are
authored on the **incarnation prefab**, mirroring `PawnAssemblyModuleRoot` /
`Point[]`:

```csharp
[Serializable]
public sealed class Socket
{
    public string Id;          // stable, e.g. "Turret", "Barrel"
    public string Kind;        // accepted ModuleKind name (inspector string/enum)
    public Transform Anchor;   // where the child view is parented
}

public sealed class SocketSet : MonoBehaviour          // on the incarnation root
{
    public IReadOnlyList<Socket> Sockets { get; }
    public bool TryGet(string id, out Socket socket);
    void OnDrawGizmos();        // axis gizmos + labels, ported from the original
}
```

The runtime resolves a parent entity's sockets through its view:
`entity.Get<Incarnation<EntityIncarnation>>().View.GetComponent<SocketSet>()`.
No new lookup infrastructure — the incarnation already holds `.View`.

### 4.3 ECS components (the graph — serialized via `IObjectSerializable`)

```csharp
// Root side. Authoritative socket -> child map. On the assembly root entity.
[Serializable]
public sealed class EntityAssembly : EosObject, IObjectSerializable
{
    // live: socketId -> child EosEntity
    // DataType: List<SocketLink>  where SocketLink { string SocketId; int ChildLocalId }
}

// Authored, intrinsic. On a module entity, advertises what it is.
[Serializable]
public sealed class Module : EosObject, IObjectSerializable
{
    public ModuleKind Kind;            // DataType: string (the kind name)
}

// Runtime link, child side. Enables O(1) detach-by-child and "am I attached".
[Serializable]
public sealed class AttachedTo : EosObject, IObjectSerializable
{
    public EosEntity Parent;           // serialized as localId
    public string SocketId;            // serialized as-is
    public Vector3    LocalPosition;   // offset within the socket anchor (serialized)
    public Quaternion LocalRotation;   // (serialized); identity == snap to anchor
    // Kind is read from the sibling Module; not duplicated here
}
```

The **local offset** (`LocalPosition`/`LocalRotation`) is per-attachment, not
per-module-type — the same scope can sit at different rail positions on different
rifles. It travels with `AttachedTo` like any other data, so it survives save/load
with the module. Identity rotation + zero position means "snap exactly to the
anchor"; that's the default when you attach without specifying an offset. This is
the EOS equivalent of the original's per-part `LocalPosition`/`LocalRotation`,
minus the absolute-transform drift (the offset is relative to the live anchor, so
editing the prefab's anchor moves the module with it).

`EntityAssembly` is the authoritative side for enumeration and cascade-destroy;
`AttachedTo` is the back-reference. The attach service keeps them in sync. Because
`AttachedTo` now also carries the per-attachment local offset (which `EntityAssembly`
does not), **both sides are serialized** and a reconciler only repairs the back-link
direction if one is missing — see §6.

Why both directions: the original kept `_modulesByRoot`, `_rootByModule`,
`_modulesByType` for exactly the two access patterns — "list a root's modules"
and "find a module's root". We get the same with two small components and no
hand-maintained dictionaries.

### 4.4 Service & extensions (the API)

```csharp
public sealed class AssemblyService          // one per World, via ServiceRegistry
{
    bool Attach(EosEntity parent, string socketId, EosEntity module);                       // snap to anchor
    bool Attach(EosEntity parent, string socketId, EosEntity module, Vector3 pos, Quaternion rot);
    bool Detach(EosEntity module);
    bool SetLocalOffset(EosEntity module, Vector3 pos, Quaternion rot);   // update offset, persists
    bool TryGetModule(EosEntity parent, string socketId, out EosEntity module);
    int  GetModules(EosEntity parent, List<EosEntity> into);   // alloc-free fill
    bool IsSocketFree(EosEntity parent, string socketId);
}

public static class AssemblyExtensions
{
    static AssemblyService Assemblies(this World world);       // lazy, cached in Services
    static bool AttachTo(this EosEntity module, EosEntity parent, string socketId);
    static bool AttachTo(this EosEntity module, EosEntity parent, string socketId, Vector3 pos, Quaternion rot);
    static bool Detach(this EosEntity module);
    static bool TryGetModule(this EosEntity parent, string socketId, out EosEntity m);
}
```

`Attach` validates in order: parent has `EntityAssembly`; parent view exposes a
`SocketSet` with `socketId`; the socket is free; `module` has a `Module` whose
`Kind` matches the socket's accepted kind. On success it records the link in
`EntityAssembly`, adds `AttachedTo` to the module (storing the local offset — zero
for the snap-to-anchor overload), reparents the module's view transform under the
socket anchor and applies that offset, and emits `ModuleAttached`. `SetLocalOffset`
nudges an already-attached module along its rail and updates the stored offset so it
survives a save. `Detach` reverses every step and emits `ModuleDetached`.

```csharp
public readonly struct ModuleAttached  { public EosEntity Parent, Module; public string SocketId; }
public readonly struct ModuleDetached  { public EosEntity Parent, Module; public string SocketId; }
```

### 4.5 Structural-change safety

Attach/detach are structural (they add/remove `AttachedTo` and mutate
`EntityAssembly`). EOS's `StructuralChangePolicy` throws on direct structural
changes during system iteration, so the service has two paths:

- **Immediate** — for setup/UI code outside the frame loop.
- **Deferred** — when called from inside a system, the op is queued onto the
  world's command buffer (`AfterUpdate` by default) and applied at the safe
  point, exactly like every other EOS structural change.

The extension methods pick the path automatically from `world.IsIterating`
(falling back to deferred when in doubt), so callers don't have to think about it.

## 5. View reparenting & its timing

Attach physically parents the module's view GameObject under the parent view's
socket anchor. The hard case is **load**: the serializer restores `AttachedTo`
links before any incarnation view exists, and a parent's view may Awake in the
same frame as the child's.

Solution: a small reactive/guarded system, not a one-shot.

```csharp
[Group(typeof(IncarnationGroup))]
[UpdateAfter(typeof(IncarnationSyncSystem))]
sealed class AssemblyViewBindSystem : EosSystem
{
    void Execute(AttachedTo link, EosEntity child)
    {
        if (link.ViewBound) return;                       // idempotent
        if (!TryResolveSocketAnchor(link, out var anchor)) return;  // parent view not ready yet
        if (!TryResolveChildView(child, out var view)) return;      // child view not ready yet
        Reparent(view, anchor, link.LocalPosition, link.LocalRotation);   // apply stored offset
        link.ViewBound = true;
    }
}
```

`ViewBound` is a transient runtime flag (not serialized). The system retries
every frame until both views exist, then binds once and goes quiet. This covers
fresh attach, load, and "parent activated late" uniformly. (`OnDispose` of the
incarnation already destroys the child view, so detach/destroy need no special
view cleanup beyond unparenting a still-living module.)

## 6. Save / load — no custom serializer

Nothing bespoke. `EntityAssembly`, `Module`, and `AttachedTo` implement
`IObjectSerializable`; `WorldSerializer.Capture/Restore` already:

- serializes them as ordinary component bags,
- writes child/parent references as `localId` ints and **remaps them on restore**
  via `IDeserializeContext.Resolve`,
- preserves tags, names, active flags, and stable keys.

`WorldSerializer.Restore` runs in **two passes** — it creates *every* entity in
the snapshot first, then adds components and calls `DeserializeData`. So by the
time any reference is resolved, every entity already exists. Restore flow:

1. All entities recreated into the id-remap table.
2. Components added; `DeserializeData` repopulates `EntityAssembly` links and
   `AttachedTo` (parent ref + socket + **local offset**), and re-interns
   `Module.Kind` from its name. Both link directions are serialized; a reconciler
   only fills in a missing back-link, never overrides the offset.
3. Once views instantiate, `AssemblyViewBindSystem` reparents each module under
   its socket anchor and applies its stored offset (§5).

### 6.1 Restore order & nested assemblies — order-independent

Because of the two-pass restore, **the logical graph reconnects regardless of
record order, at any nesting depth**. A turret is both `AttachedTo` (on the tank)
and `EntityAssembly` (holding a cannon); both are plain components resolved in the
same pass, so "modules that are themselves sockets" need no depth sorting.

View reparenting is order-independent too: Unity transform parenting is per-link
(`cannon → turretSocket` and `turret → tankSocket` compose to the same hierarchy
in any order), and `AssemblyViewBindSystem` binds **each link independently the
moment its own two endpoints exist**, retrying others next frame. Depth *N* is
just *N* self-healing links — no top-down pass.

This also drops a whole failure class from the original, which re-found parts by a
string `HierarchyPath` (`FindTransformByPath`) and silently fell back to the root
when a path didn't resolve. We store direct entity references + a socket id, so
renaming a transform in a prefab can't misplace a module.

### 6.2 Where placement lives — heads-up

In EOS, `WorldSerializer` saves **only `EosObject` components** — a Unity view's
transform is **not** auto-serialized for anyone. So:

- A **module's** placement needs nothing extra: its local offset rides in
  `AttachedTo`, and its world placement is derived by reparenting under the root.
- A **root's** (or any free-standing entity's) world placement persists only if
  you model it as a serialized component (e.g. a `Position`/`Transform` component
  the view reads in `OnSync`). This is the general EOS.Unity contract — the view
  is a projection of ECS — not a quirk of assemblies. The original got away
  without it only because its bespoke serializer hand-wrote `Position`/`Rotation`;
  here that belongs in a component, which then round-trips for free.

## 7. Authoring

- **A module** is an `EntityPreset` whose component list includes
  `Module { Kind = ModuleKind.Of("Scope") }`, and whose incarnation prefab is the
  visual. Spawn with `preset.Instantiate()` like any entity.
- **An assembly root** is an `EntityPreset` whose incarnation prefab carries a
  `SocketSet`. Build it, then attach modules:

```csharp
var rifle  = riflePreset.Instantiate();
var scope  = scopePreset.Instantiate();
rifle.TryGetModule("Optics", out _);            // false, empty
scope.AttachTo(rifle, "Optics");                // snap to anchor, reparent view, fire event
// nudge it forward along the rail; the offset is saved with the module:
world.Assemblies().SetLocalOffset(scope, new Vector3(0, 0, 0.03f), Quaternion.identity);
// ... later
scope.Detach();                                 // or Destroy(rifle) cascades to modules
```

Cascade: destroying a root walks `EntityAssembly` and destroys (Phase 1) or
returns to pool (Phase 3) each module first, depth-first — the original did this
in `OnDestroy`/`OnPushed`.

## 8. Mapping table (original → port)

| `SackranyPawnAssembly` | `EOS.Unity` (this module) |
|---|---|
| `Pawn` / `Limb` | `EosEntity` / `EosObject` |
| `PawnAssemblyReference` + `ResourcesCache` | `IncarnationDatabase` (reused) |
| `PawnAssemblyRuntimeRegister` | EOS stable keys (reused) |
| `PawnAssembly` (root) | `EntityAssembly` |
| `PawnAssemblyModuleRoot` + `Point` | `SocketSet` + `Socket` |
| `IAssemblyModuleType` + `TypeRegistry` | `ModuleKind` + `ModuleKindRegistry` |
| `AssemblyModuleType` (limb) | `Module` |
| `AssemblyDynamicModules` | `AssemblyService` + `AttachedTo` + events + bind system |
| `PawnAssemblyEvents` | `ModuleAttached` / `ModuleDetached` (EOS events) |
| `PawnAssemblyPool` | `IncarnationPool` (Phase 3) |
| `PawnAssemblySerializer` + data classes | **deleted** — `WorldSerializer` (reused) |
| `IAssemblyLimb` | entity refs auto-restored + `AssemblyViewBindSystem` |

## 9. Phasing

**Phase 1 — core graph (the deliverable after this doc is approved):**
`ModuleKind` + registry; `Socket` / `SocketSet` (+ gizmos); `EntityAssembly`,
`Module`, `AttachedTo` (incl. per-attachment local offset); `AssemblyService`
(attach with/without offset, `SetLocalOffset`, detach/query, immediate +
deferred); `AssemblyViewBindSystem`; cascade destroy; `ModuleAttached/Detached`
events; save/load through `WorldSerializer` (no custom serializer); editor
drawer for the `Module.Kind` / `Socket.Kind` fields and `SocketSet` inspector.

**Phase 2 — authoring ergonomics:** default modules on an assembly preset
(`List<(socketId, EntityPreset)>`, spawned + attached when the root is created
new — the original's `LoadDefaultModules` gated on `IsNew`); "find unlisted
modules" reconciliation for hand-placed children; `ModuleKind` enum picker;
preset-inspector affordances (socket list shown read-only from the prefab).

**Phase 3 — pooling:** `IncarnationPool` keyed by incarnation id
(`Pop` / `Push` / `PreWarm`), and detach/cascade returning modules to the pool
instead of destroying. Deferred because EOS pools nothing today and reuse has to
be reconciled carefully with `EosObject` dispose/reset semantics — worth doing
once the graph is proven, not before.

Note: with the per-attachment local offset living on `AttachedTo`, both link
directions are serialized anyway (the offset only exists child-side), so the
"serialize one side + reconcile" option is off the table — the reconciler now only
repairs a missing back-link, it is not an alternative encoding.

## 10. Decisions (locked)

1. **Module/socket typing** — a dedicated `ModuleKind` value type.
2. **View hierarchy** — child view transforms are reparented under socket anchors.
3. **Per-attachment local offset** — stored on `AttachedTo`, serialized; runtime
   `SetLocalOffset`; editor side bakes the live local transform with one click.
4. **`AssemblyService` location** — one per `World`, registered in
   `World.Services` via `ServiceRegistry`, reached through `world.Assemblies()`
   (no static/global state; clean across worlds and domain reload).
5. **Socket occupancy** — one module per `socketId`; multiple sockets may share a
   `ModuleKind`.
6. **`ModuleKind` authoring** — free strings interned + serialized by name, plus
   `ModuleKind.Of<TEnum>` for enum-typed kinds. (A registry-asset dropdown is a
   possible later convenience, not Phase 1.)
7. **World scope** — assemblies are world-local: root and all modules live in the
   same `World` (entity refs and stable keys are world-local).

All design questions are resolved; Phase 1 (§9) is ready to implement on approval.
