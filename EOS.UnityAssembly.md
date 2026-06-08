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
    // Kind is read from the sibling Module; not duplicated here
}
```

`EntityAssembly` is the authoritative side for enumeration and cascade-destroy;
`AttachedTo` is the back-reference. The attach service keeps them in sync, and on
restore `AttachedTo` is re-derived from `EntityAssembly` (serialize one side,
rebuild the other) to make desync impossible — see §6.

Why both directions: the original kept `_modulesByRoot`, `_rootByModule`,
`_modulesByType` for exactly the two access patterns — "list a root's modules"
and "find a module's root". We get the same with two small components and no
hand-maintained dictionaries.

### 4.4 Service & extensions (the API)

```csharp
public sealed class AssemblyService          // one per World, via ServiceRegistry
{
    bool Attach(EosEntity parent, string socketId, EosEntity module);
    bool Detach(EosEntity module);
    bool TryGetModule(EosEntity parent, string socketId, out EosEntity module);
    int  GetModules(EosEntity parent, List<EosEntity> into);   // alloc-free fill
    bool IsSocketFree(EosEntity parent, string socketId);
}

public static class AssemblyExtensions
{
    static AssemblyService Assemblies(this World world);       // lazy, cached in Services
    static bool AttachTo(this EosEntity module, EosEntity parent, string socketId);
    static bool Detach(this EosEntity module);
    static bool TryGetModule(this EosEntity parent, string socketId, out EosEntity m);
}
```

`Attach` validates in order: parent has `EntityAssembly`; parent view exposes a
`SocketSet` with `socketId`; the socket is free; `module` has a `Module` whose
`Kind` matches the socket's accepted kind. On success it records the link in
`EntityAssembly`, adds `AttachedTo` to the module, reparents the module's view
transform under the socket anchor (local pos/rot/scale zeroed, as the original
did), and emits `ModuleAttached`. `Detach` reverses every step and emits
`ModuleDetached`.

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
        Reparent(view, anchor);
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

Restore flow:

1. `WorldSerializer.Restore` recreates entities and components; `DeserializeData`
   repopulates `EntityAssembly` links (child refs resolved) and `Module.Kind`
   (re-interned from name).
2. `AttachedTo` is **rebuilt from `EntityAssembly`** by a `[New] EntityAssembly`
   reconciler so the two sides can't disagree (we only trust the root-side map on
   disk). *(Alternative: serialize both and skip the reconciler — noted as a
   smaller-code option in §9.)*
3. Once views instantiate, `AssemblyViewBindSystem` reparents them (§5).

Position/rotation are **not** stored centrally as the original did — each entity
carries its own transform state through its incarnation/components, and modules
inherit world placement from being reparented under the root. One source of
truth, no drift.

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
scope.AttachTo(rifle, "Optics");                // validates kind, reparents view, fires event
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
`Module`, `AttachedTo`; `AssemblyService` (attach/detach/query, immediate +
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

**Smaller-code alternative (call before Phase 1 starts):** serialize **both**
`EntityAssembly` and `AttachedTo` and drop the §6 reconciler. Saves a system at
the cost of two on-disk copies of each link that must agree. Recommendation:
keep the reconciler (single source of truth on disk), but it's a one-line switch.

## 10. Open points for review

1. **`AssemblyService` location** — per-world via `ServiceRegistry` (recommended,
   matches EOS conventions) vs. a static facade like the original's managers.
2. **Single vs. multi-occupancy sockets** — proposed: one module per `socketId`
   (matches `Point`); multiple sockets may share a `ModuleKind`.
3. **Reconciler vs. double-serialize** (§9 alternative).
4. **Cross-world** — assemblies are within one world (entity refs and stable keys
   are world-local). Confirm that's acceptable.
5. **`ModuleKind` authoring** — free strings vs. an enum/registry asset for the
   inspector dropdown. Strings ship in Phase 1; a picker can come in Phase 2.
