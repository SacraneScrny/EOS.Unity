# EOS.Unity — Unity bridge for the EOS ECS

Engine-integration layer for the [EOS](../EOS) core. The core stays engine-free; this package is the consumer assembly that drives the `Universe` from Unity's PlayerLoop, plugs the view (incarnation) layer into prefabs, adds ScriptableObject authoring for entities, a runtime entity-assembly (socket/module) layer, view pooling, attribute-driven bootstrap codegen, and editor tooling.

The core is **never modified** by this package — it only attaches through the existing seams: `Universe`, `IncarnationBridge`, `WorldBootstrap`, `EosLog`, `EosProfiler`, `WorldSerializer`/`WorldLoader`.

---

## Table of contents

- [Requirements](#requirements)
- [Install](#install)
- [Quick start](#quick-start)
- [Folder convention](#folder-convention)
- [Boot](#boot)
- [Attribute boot (`[EosBoot]`)](#attribute-boot-eosboot)
- [World bootstrap (`[EosWorldBootstrap]`)](#world-bootstrap-eosworldbootstrap)
- [Domain reset (`[EosDomainReset]`)](#domain-reset-eosdomainreset)
- [Incarnations (views)](#incarnations-views)
- [View pooling](#view-pooling)
- [Entity presets (ScriptableObject)](#entity-presets-scriptableobject)
- [Component sets](#component-sets-required-shared-archetypes)
- [Entity assemblies (sockets & modules)](#entity-assemblies-sockets--modules)
- [Saves](#saves)
- [Logging / profiling / debug draw](#logging--profiling--debug-draw)
- [Editor tools](#editor-tools)
- [Notes & caveats](#notes--caveats)

---

## Requirements

- The **EOS core** sources present in the same Unity project.
- Unity with the `UnityEngine.LowLevel` PlayerLoop API (2019.3+).
- **MackySoft.SubclassSelector** — required for the entity preset inspector (`[SerializeReference]` component picker). Install via UPM:
  `https://github.com/mackysoft/Unity-SerializeReferenceExtensions.git?path=Assets/MackySoft/MackySoft.SerializeReferenceExtensions`
  (Package Manager → Add package from git URL).
- The incarnation index uses `JsonUtility` — no Newtonsoft dependency in the bridge itself.

## Install

Drop the `EOS.Unity` folder anywhere under `Assets/`. Layout:

```
EOS.Unity/
  Runtime/    -> compiles into Assembly-CSharp (no asmdef)
  Editor/     -> compiles into Assembly-CSharp-Editor (Unity 'Editor' folder)
```

No asmdef is shipped on purpose: the core has none, so everything lands in `Assembly-CSharp` and sees each other (including the core's `internal` members). If you later move the core into an asmdef, add one here too and reference the core's asmdef.

## Quick start

1. Let the editor create the folder layout (happens automatically on load), or run **Sackrany ▸ EOS ▸ Create EOS Resources**.
2. Generate a bootstrap: **Sackrany ▸ EOS ▸ Create Default Bootstrap**, drop the generated `GameBootstrap` component on a GameObject in your first scene.
3. Author a view prefab under `Assets/Resources/Incarnations/` (the index rebuilds automatically).
4. Create an **Entity Preset** (`Assets > Create > Sackrany > EOS > Entity Preset`), pick the incarnation id, add components.
5. Drop an **Entity Preset Spawner** (`Sackrany/EOS/Entity Preset Spawner`) into the scene, assign the preset, press Play.
6. Open **Sackrany ▸ EOS ▸ World Inspector** to watch the live world.

## Folder convention

Incarnation prefabs live under a dedicated Resources folder — the deliberate separation of incarnation content from the rest of the project:

```
Assets/Resources/Incarnations/...        <- your incarnation prefabs
Assets/Resources/EntityPresets/...       <- your EntityPreset assets (convenience)
Assets/Resources/ModuleKindCatalog.asset <- empty catalog, fill the kinds yourself
Assets/Resources/incarnations.json       <- generated index (do not edit by hand, except Redirects)
```

This layout is created automatically when the editor loads (or scripts recompile) if it doesn't already exist — no manual setup needed. You can also recreate it on demand via **Sackrany ▸ EOS ▸ Create EOS Resources**. Only the folders and an empty `ModuleKindCatalog` are created (and only if no catalog exists anywhere yet); the prefabs, presets, and the catalog's kind list are yours to author.

The index is rebuilt automatically by an `AssetPostprocessor` whenever anything under `Resources/Incarnations` changes, and on demand via **Sackrany ▸ EOS ▸ Rebuild Incarnation Index**. Browse it read-only via **Sackrany ▸ EOS ▸ Incarnation Database**.

The **id** of an incarnation is its path under `Incarnations/` without extension (`Resources/Incarnations/Enemies/Orc.prefab` → id `Enemies/Orc`). This id is what you pass to `Setup(...)`, what presets store, and what gets serialized into saves.

## Boot

### What runs automatically

Static setup runs at `SubsystemRegistration` (before any scene loads), every play session:

1. `EosDomainReset.Reset()` — core static state cleared.
2. `UnityLogHandler.Install()` — `EosLog` mirrored to the Unity console.
3. `EosDomainResetRunner.Run()` — your `[EosDomainReset]` methods (see below).
4. Default incarnation binders registered (`GameObject`, `EntityIncarnation`).
5. `Application.quitting` hooked → `EosLoop.Shutdown()` on quit.

The editor also tears the loop down on exiting Play Mode, so "Enter Play Mode Options" without domain reload stays clean.

**Starting the world is explicit** — nothing simulates until you call `EosLoop.Boot(...)` (directly, via the generated `GameBootstrap`, or via attribute boot).

### Default bootstrap (recommended)

**Sackrany ▸ EOS ▸ Create Default Bootstrap** opens a generator window: pick a folder (and optionally a class name / namespace) and it writes a `GameBootstrap` MonoBehaviour with `[DefaultExecutionOrder(-10000)]`, an `IsBooted` guard, and a serialized `EosBootConfig` field — drop it on one GameObject in your first scene and tune options in the inspector. It won't overwrite an existing file.

### Manual boot

```csharp
using EOS.Unity;

public static class Game
{
    public static void Start()
    {
        var config = new EosBootConfig
        {
            EnableProfiler = false,                    // true -> ProfilerMarker backend
            DebugDraw = true,                          // hidden Gizmos drawer (play mode only)
            MinLogLevel = EOS.Logging.LogLevel.Debug,  // console filter
        };

        // register custom view binders (defaults for GameObject and
        // EntityIncarnation are already registered):
        config.AddBinder(new MyShipBinder());          // chainable

        EosLoop.Boot(config);
    }
}
```

`EosLoop.Boot(config = null)` is idempotent (`EosLoop.IsBooted` guards re-entry) and runs, in order: log level → profiler backend → `IncarnationDatabase.Load()` → custom binders → `Universe.Boot()` → PlayerLoop install → debug drawer (if enabled).

### PlayerLoop integration

`EosLoop.Boot` injects three nodes, each at the **start** of its PlayerLoop stage:

| EOS call | PlayerLoop stage |
|---|---|
| `Universe.FixedUpdate(Time.fixedDeltaTime)` | `FixedUpdate` |
| `Universe.Update(Time.deltaTime)` | `Update` |
| `Universe.LateUpdate(Time.deltaTime)` | `PreLateUpdate` |

Injection is idempotent (own nodes are removed before re-inserting). Teardown happens on `Application.quitting` and on exiting Play Mode in the editor. Pause is free: `timeScale = 0` yields `dt = 0` and stops Unity's FixedUpdate. `EosLoop.Shutdown()` also clears view pools and unloads the incarnation database.

## Attribute boot (`[EosBoot]`)

For multi-system startup you don't have to wire a boot method by hand. Create the orchestrator once via **Sackrany ▸ EOS ▸ Create Auto Bootstrap** — that generates `Assets/EOS.Generated/EosBootstrap.gen.cs`, which auto-runs at `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)`. **Its existence is the opt-in**: the file is never created automatically, but while it exists the editor keeps it in sync on every recompile (rewriting only when content actually changes). Even with zero `[EosBoot]` steps it still boots EOS — the first step is always the built-in `EosLoop.Boot()`.

Tag any `public static` parameterless method with `[EosBoot]` and it is collected, sorted, and called after boot — each guarded so one throwing step doesn't abort the rest:

```csharp
using EOS.Unity;

public static class GameSystems
{
    [EosBoot(order: -100)]                       // lower order runs earlier
    public static void RegisterBinders() { /* ... */ }

    [EosBoot]
    [EosBootAfter(typeof(GameSystems), nameof(RegisterBinders))]
    public static void LoadConfig() { /* ... */ }

    [EosBoot(isFallback: true)]                  // also runs on the warm path
    public static void EnterScene() { /* ... */ }
}
```

Ordering:

- `[EosBoot(order: N)]` — coarse key, lower runs earlier (ties broken deterministically by type/method name).
- `[EosBootBefore(typeof(X))]` / `[EosBootAfter(typeof(X))]` — hard constraints relative to **all** boot methods of `X`. Add a method name — `[EosBootAfter(typeof(X), nameof(X.Step))]` — to target a single method when a class has several. Constraints win over `Order` (topological sort); a cycle is logged and the offenders fall back to `Order`.

**Warm path / fallback:** the generated `Run()` is `public`, so you can call it again later (e.g. per-scene re-entry). If EOS is already booted it runs **only** the steps marked `isFallback: true` and skips `EosLoop.Boot()` — "the core is up but I still need to re-run my per-scene setup". A method marked `isFallback: true` runs on both the cold and warm paths.

### Configuring boot (`[EosBootConfigProvider]`)

To customise the config the generated boot uses, tag a `public static EosBootConfig Name(EosBootConfig config)` method with `[EosBootConfigProvider]`. Any number of systems can contribute: the generated bootstrap threads **one** config instance through every provider (sorted by `Order`, plus `[EosBootBefore]`/`[EosBootAfter]` resolved among providers) and feeds the result into `EosLoop.Boot(config)` — before any `[EosBoot]` step.

```csharp
using EOS.Unity;

public static class Rendering
{
    [EosBootConfigProvider(order: -10)]
    public static EosBootConfig Configure(EosBootConfig c)
    {
        c.EnableProfiler = true;
        c.AddBinder(new MyShipBinder());   // register custom view binders here
        return c;                          // returning null keeps the incoming config
    }
}
```

Providers run only on the cold path. Mutate and return the same instance, or return a new one — both work.

Notes:

- With no `[EosBootConfigProvider]`, boot uses a default `EosBootConfig`. (The `GameBootstrap` component / **Create Default Bootstrap** generator remain available for inspector-driven, non-attribute boot.)
- `[EosBoot]` methods must be `public static` parameterless, and `[EosBootConfigProvider]` methods `public static EosBootConfig (EosBootConfig)`, both on a public non-generic type — other shapes are skipped with a warning so the generated file always compiles.
- To turn attribute boot off, delete `EosBootstrap.gen.cs` — without it nothing is generated and boot stays fully explicit. The file can be git-ignored; re-create it with the same menu item. Re-running the command on an existing file just refreshes it.

## World bootstrap (`[EosWorldBootstrap]`)

Per-**world** setup — registering services, seeding context defaults — has its own attribute and codegen, separate from `[EosBoot]` (which is per-**play-session**). Create the generated file once via **Sackrany ▸ EOS ▸ Create World Bootstrap** → `Assets/EOS.Generated/WorldBootstrap.gen.cs` (same opt-in-by-existence rule; kept in sync on recompile).

Tag a `public static void Name(World world)` method:

```csharp
using EOS.Core;
using EOS.Unity;

public static class GameServices
{
    [EosWorldBootstrap(order: -10)]
    public static void Register(World world)
    {
        world.ServiceRegistry.Register<IPathfinder>(new GridPathfinder());
        world.Context.Set(new Difficulty { Level = 1 });
    }
}
```

The generated file installs itself into `EOS.Loader.WorldBootstrap.Provider` at `SubsystemRegistration` (before any world exists). Every `World.Init()` **and** `World.Reset()` then runs all collected methods — for the default world, for every `Universe.CreateWorld(...)`, and again after resets, so your services and context defaults are always present. Sorted by `order`, ties broken by type/method name. Invalid shapes are skipped with a warning.

> The built-in `AssemblyService` (see [Entity assemblies](#entity-assemblies-sockets--modules)) registers itself through this same mechanism.

## Domain reset (`[EosDomainReset]`)

Static state that must be cleared between play sessions (when "Enter Play Mode Options" disables domain reload) can be tagged with `[EosDomainReset]` on a `static` parameterless `void` method. These run at `SubsystemRegistration`, right after the core reset, discovered by **reflection** (no codegen, no ordering guarantees — any access modifier is fine):

```csharp
static class Scoreboard
{
    static int _highScore;

    [EosDomainReset]
    static void Reset() => _highScore = 0;
}
```

> **IL2CPP:** reflection-invoked methods can be stripped — add `[UnityEngine.Scripting.Preserve]` (or a `link.xml` entry) to `[EosDomainReset]` methods in stripped builds. `[EosBoot]` / `[EosWorldBootstrap]` steps are called directly by generated code, so they are never stripped.

## Incarnations (views)

Two binders are registered by default:

- **`GameObject`** — spawn-and-forget visual, no sync logic.
- **`EntityIncarnation`** — a `MonoBehaviour` base with lifecycle hooks; the view pulls its data straight from ECS.

Author a view by subclassing `EntityIncarnation` on the prefab root:

```csharp
using EOS.Unity;
using EOS.Extensions;
using UnityEngine;

public sealed class OrcView : EntityIncarnation
{
    protected override void OnBind()   { /* spawned, Entity is set */ }

    protected override void OnSync()   // every Update; also OnSyncFixed / OnSyncLate
    {
        if (Entity.TryGet<Position>(out var p))
            transform.position = new Vector3(p.X, p.Y, p.Z);
    }

    protected override void OnUnbind() { /* about to be destroyed/despawned */ }
}
```

Attach a view to an entity through the closed-generic incarnation component:

```csharp
using EOS.Objects;

var inc = entity.Add<Incarnation<EntityIncarnation>>();
inc.Setup("Enemies/Orc");   // id from the index
entity.On();                // activate -> Awake instantiates the prefab
```

For a logic-less visual, use `Incarnation<GameObject>` instead. Custom view types are one binder away: implement `IIncarnationBinder<TMyView>` and register it via `config.AddBinder(...)` (or `IncarnationBridge.Register`).

Resolution path: `IncarnationDatabase.Resolve(id)` follows redirects (rename support, 64-hop cycle guard), loads the prefab from `Resources` at the indexed path, and caches it. Both built-in binders spawn through `ViewPoolRegistry`, which transparently enables pooling (next section).

## View pooling

Pooling is **opt-in per prefab**: add the **Incarnation Pooling** component (`Sackrany/EOS/Incarnation Pooling`) to a prefab root and configure:

- **Preload** — instances created up front when the pool is first used (default 0).
- **Max Size** — pool capacity; returned instances beyond it are destroyed (default 32).

Prefabs without the component instantiate/destroy normally — no behaviour change. With it, both default binders rent from and return to a per-prefab `GameObjectPool` (pooled instances are parked deactivated under a hidden `EOS View Pool` object).

A pooled view usually needs to reset itself between uses. Implement `IPoolableView` on any component of the prefab (children included):

```csharp
using EOS.Unity;
using UnityEngine;

public sealed class TracerView : MonoBehaviour, IPoolableView
{
    TrailRenderer _trail;
    void Awake() => _trail = GetComponent<TrailRenderer>();

    public void OnRent()   => _trail.Clear();   // just rented, about to be used
    public void OnReturn() { /* released back to the pool */ }
}
```

`OnRent` fires on every spawn from the pool (including the first), `OnReturn` on every despawn into it. For `EntityIncarnation` views, `OnBind`/`OnUnbind` still fire per entity as usual — pooling only changes where the GameObject comes from.

Programmatic access (`EOS.Unity.ViewPoolRegistry`):

```csharp
ViewPoolRegistry.GetOrCreate(prefab, maxSize: 64).Prewarm(16);
GameObject view = ViewPoolRegistry.Spawn(prefab);    // pooled or plain Instantiate
ViewPoolRegistry.Despawn(view);                      // back to pool, or Destroy
ViewPoolRegistry.Clear(prefab);
ViewPoolRegistry.ClearAll();                         // also runs on shutdown & domain reset
```

## Entity presets (ScriptableObject)

Instead of hand-writing the create-entity / add-components / configure / set-incarnation / activate dance for every kind of object, author it once as a **preset** asset and spawn it anywhere.

Create one via `Assets > Create > Sackrany > EOS > Entity Preset`. The inspector holds everything needed to boot an entity:

- **Name / Active / Serializable** — the `EosEntity` flags.
- **Incarnation** — view kind (`EntityIncarnation`, `GameObject`, or `None`) and an id. The id field has a dropdown sourced from `incarnations.json`, so you select from real prefabs.
- **Tags** — a plain string list.
- **Components** — a `[SerializeReference]` list of `EosObject` subclasses, configured inline. **Add Component** opens a searchable type picker over every concrete component in the project; field values you set on the asset are the spawn defaults.
- **Component Sets** — shared required bundles (next section).
- **Default Modules** — socket-attached sub-entities (see [Entity assemblies](#entity-assemblies-sockets--modules)).

Spawn it from code:

```csharp
using EOS.Unity;

[SerializeField] EntityPreset _orc;

void SpawnOrc()
{
    var entity = _orc.Instantiate();          // into the default world
    // or _orc.Instantiate(myWorld);
}
```

…or drop an **Entity Preset Spawner** component (`Sackrany/EOS/Entity Preset Spawner`) on a GameObject: assign the preset and it spawns on `Start` (EOS must already be booted — the spawner logs an error otherwise). Options: *Spawn On Start* (default on), *Destroy After Spawn*; the result is exposed as `LastSpawned`, and `Spawn()` can be called manually. There's also a **Spawn Into Default World** button on the preset inspector while in Play Mode.

**Spawn order** inside `Instantiate`: create the entity **inactive** → apply component sets (respecting overrides) → tags → incarnation component → the preset's own components → activate (if *Active*) → spawn-and-attach default modules. So `Awake`/`Start` (and the view instantiation) always see the final configured data.

**How configured data reaches the live component:** the preset stores each component as a serialized template; at spawn the matching storage is resolved by type, a fresh component is added to the entity, and the template's fields are **deep-copied** onto it. The copy is *Unity-serialization-aligned* — it copies exactly the fields Unity would serialize (public or `[SerializeField]`/`[SerializeReference]`, not static/readonly/`[NonSerialized]`), deep-clones arrays, `List<>`s, `[Serializable]` classes and non-plain structs (depth-capped), and passes `UnityEngine.Object` references through as-is. Entities never share mutable reference-type fields with the asset or with each other.

For components to show up and be editable, mark them `[Serializable]`:

```csharp
using System;
using EOS.Objects;

[Serializable]
public sealed class Health : EosObject
{
    public int Max = 100;
    public int Current = 100;
}
```

Inspector ergonomics: the preset is split into **Info** (name, flags, incarnation) and **Data** (tags, sets, components) foldouts with per-asset persisted state. Components are edited as individual titled blocks, not a raw list — **Add Component** opens the type picker; a block's type is fixed after adding (delete and re-add to change); each block has **✕** (delete, confirmed) and **Revert** (reset to type defaults, confirmed).

## Component sets (required, shared archetypes)

Some entity types share a fixed set of components that must always be present — miss one and a system's query silently skips the entity, which is a nasty bug to track down. A **Component Set** is a separate asset that bundles such a set; any number of presets reference it.

Create one via `Assets > Create > Sackrany > EOS > Component Set` — it holds the same kind of `[SerializeReference]` component list (plus tags) as a preset. Then, on any `EntityPreset`, add the set under **Component Sets ▸ Sets**.

What you get:

- **Always applied.** At spawn, every referenced set's components (and tags) are added to the entity, before the preset's own extras.
- **Can't be dropped by accident.** Set components show under *"Set Components (required)"* in the preset inspector as read-only — there is no per-component delete. To remove them you remove the *whole* set from the list.
- **Synced.** Edit the set asset once and every preset that references it picks up the change (structure and values).
- **Locally overridable.** Press **Override** on a set component to materialize a local, editable copy stored in the preset; its values win for that preset only. **Revert** drops the override and re-syncs to the set. Overrides are keyed by component type (one component per type per entity, same as storage), and orphaned overrides are pruned automatically when a set or component goes away.
- **Composable.** A preset can reference several sets (e.g. `Damageable` + `Movable`). Sets apply in list order, then the preset's own `Components` list — one component per type per entity throughout, so use **Override** (not a duplicate entry) to change a set component's values.

Spawning is unchanged — `preset.Instantiate()` resolves sets, overrides, extras, tags, incarnation, and default modules into one entity.

### Code modules

Sometimes a set is easier to express in code than to author as data. Rather than subclassing the ScriptableObject (single inheritance, one asset per behaviour), a Component Set has an optional **code module**: a plain `[Serializable]` class that builds into the set. The set asset stays one sealed type — drop it into a preset's **Sets** field as usual; the module just adds to what it contributes.

```csharp
using System;
using EOS.Unity;
using UnityEngine;

[Serializable]
public sealed class EnemyBaseModule : ComponentSetModule
{
    [SerializeField] int _health = 100;

    public override void Build(ComponentSetBuilder b)
    {
        var hp = b.Add<Health>();      // returns the template to configure
        hp.Max = hp.Current = _health;

        b.Add(new Movement { Speed = 3f });
        b.Add<AiBrain>();
        b.AddTag("Enemy");
    }
}
```

In the Component Set inspector, the **Code Module** slot has a **Set Module** button (searchable type picker over every `ComponentSetModule`); once assigned it shows the module's `[SerializeField]` fields, with **Change** / **clear**.

Builder API: `Add<T>()` (creates and returns the template), `Add<T>(instance)` / `Add(instance)`, `AddTag(stringOrEnum)`. A set contributes its serialized components/tags **and** its module's, then everything flows through the same merge, override, and de-dupe path. In a preset's inspector, module-built components appear under *"Set Components (required)"* by type with an **Override** button (their values live in code, so press Override to edit them locally).

## Entity assemblies (sockets & modules)

Runtime composition of entities from typed, swappable modules — a rifle with scope/grip sockets, a tank with turret and cannon — with attach/detach at runtime, full save/load, and the view hierarchy following along. Design doc: [`EOS.UnityAssembly.md`](./EOS.UnityAssembly.md).

### The model

- **`ModuleKind`** — a lightweight typed key for socket/module compatibility. Interned to an int for fast compare, **serialized by name** (save-stable — adding kinds can't corrupt old saves). `ModuleKind.Of("Scope")`, `ModuleKind.Of(MyEnum.Barrel)`, `ModuleKind.None`.
- **`SocketSet` + `Socket`** (`Sackrany/EOS/Socket Set`) — authored on the **incarnation prefab** root. Each socket: `Id` (stable string), `Kind` (accepted kind name, dropdown via `[ModuleKindField]`), `Anchor` (the transform a child view is parented under). Draws axis gizmos + labels in the scene view.
- **`Module`** (component) — on a module entity, advertises what it *is* (`Kind`). Serialized by kind name.
- **`EntityAssembly`** (component) — on the root entity; the authoritative socket→child map. `IsFree(socketId)`, `TryGetModule(socketId, out e)`, `Collect(list)`. Destroying the root **cascades** to all attached modules.
- **`AttachedTo`** (component) — the child-side back-reference: `Parent`, `SocketId`. The per-attachment **local offset** lives in the module's `EntityTransform` (`LocalPosition`, `LocalRotation`, `LocalScale`) and survives saves — the same scope can sit at different rail positions on different rifles.

### Kind authoring

A `ModuleKindCatalog` asset (`Assets > Create > Sackrany > EOS > Module Kind Catalog`; one is created in `Resources` automatically) lists your project's kinds. Every `Module.Kind` / `Socket.Kind` field renders as a dropdown sourced from all catalogs (with free-text fallback) — no typos, still open.

### Runtime API

One `AssemblyService` per world, auto-registered via `[EosWorldBootstrap]`. Use it through extensions:

```csharp
using EOS.Unity;

var rifle = riflePreset.Instantiate();
var scope = scopePreset.Instantiate();

rifle.TryGetModule("Optics", out _);            // false, socket empty
scope.AttachTo(rifle, "Optics");                // snap to anchor, reparent view, fire event
// or with an explicit local offset within the socket anchor:
scope.AttachTo(rifle, "Optics", new Vector3(0, 0, 0.03f), Quaternion.identity);

// nudge an attached module along its rail; the offset is saved with it
// (stored in the module's EntityTransform):
world.Assemblies().SetLocalOffset(scope, new Vector3(0, 0, 0.05f), Quaternion.identity);

scope.DetachFromSocket();                       // or Destroy(rifle) cascades to modules
```

`Attach` validates in order: parent has `EntityAssembly`; the parent's view exposes a `SocketSet` containing `socketId`; the socket is free; the module's `Module.Kind` matches the socket's accepted kind. On success it records the link both ways, reparents the module's view under the socket anchor (applying the offset), and emits a `ModuleAttached` event; `Detach` reverses everything and emits `ModuleDetached`. Both are EOS events (`EventExecute(ModuleAttached e)`).

**Structural-change safety:** attach/detach are structural. Called outside the frame loop they apply immediately; called from inside a system they are automatically deferred onto `World.AfterCurrentPhase` and re-validated at the safe point — callers never need to think about it.

### Views & timing

View reparenting is handled by `AssemblyViewBindSystem` (in `IncarnationGroup`): for every `AttachedTo` link whose view isn't bound yet, it retries each frame until **both** endpoint views exist, then parents the child view under the socket anchor with the stored offset and goes quiet. This uniformly covers fresh attaches, save-game loads (links restore before views instantiate), and late-activated parents. A kind mismatch discovered at bind time detaches the module rather than misplacing it.

### Default modules (presets)

An assembly-root preset can pre-fill its sockets: the **Default Modules** list pairs a socket id (dropdown read from the incarnation prefab's `SocketSet`) with a module `EntityPreset`. They spawn and attach inside `Instantiate` — which never runs during snapshot restore, so a loaded assembly restores its *saved* modules and does **not** re-apply defaults. Nesting works (a module can itself be an assembly); a depth guard prevents preset cycles.

### Save / load

Nothing bespoke: `EntityAssembly`, `Module`, and `AttachedTo` are ordinary `IObjectSerializable` components, so `WorldSerializer.Capture/Restore` round-trips the whole graph — entity references remap automatically, kinds re-intern by name, offsets ride along, and views rebind through the system above. Nested assemblies reconnect order-independently at any depth.

> The serializer saves **only EOS components** — a view transform is never auto-saved. A module's placement rides in `AttachedTo`; a free-standing root's world position persists only if you model it as a serialized component the view reads in `OnSync`. The view is a projection of ECS.

## Saves

This package adds no save logic — wire the core's two hooks to whatever persistence you use:

```csharp
using EOS.Serialization;

WorldLoader.OnSave = snapshot => /* write UniverseSnapshot with your serializer */;
WorldLoader.OnLoad = () => /* read it back, or return null for a fresh start */;
// Universe.Boot() (inside EosLoop.Boot) restores a non-null OnLoad result automatically.
```

With a `SackranySerializable`-style `DataManager` it is two lines — `TypeNameHandling.Auto` + `AssemblyFormatHandling.Simple` round-trips the snapshot version-tolerantly with no custom converters:

```csharp
using EOS.Serialization;
using SackranySerializable;

// register once (e.g. in your bootstrap):
DataManager.OnCollectSaveData += list => list.Add(WorldSerializer.Capture());

// load on your own trigger:
var snap = DataManager.Get<UniverseSnapshot>();
if (snap?.Worlds?.Count > 0) WorldSerializer.Restore(snap);
```

## Logging / profiling / debug draw

- **Logging:** `EosLog` is mirrored to the Unity console as `[EOS:source] msg` (`Error`→`LogError`, `Warning`→`LogWarning`, `Debug`→`Log`). `EosLog.Debug` is `[Conditional("DEBUG")]`, so it is stripped from non-development builds. Filter with `EosBootConfig.MinLogLevel` (or `UnityLogHandler.MinLevel` directly).
- **Profiling:** off by default (zero overhead). With `EnableProfiler = true`, a `ProfilerMarker`-based backend is installed (cached markers, stack-balanced begin/end) — world phases and every system body show up in the Unity Profiler.
- **Debug draw:** with `DebugDraw = true` (default), a hidden (`HideAndDontSave`) MonoBehaviour forwards `OnDrawGizmos` to `Universe.DebugDraw()` — your `OnDebugDraw` overrides on components/systems draw with `Gizmos`. Play mode only, editor Scene/Game view only.

## Editor tools

All under the **Sackrany ▸ EOS** menu:

| Menu item | What it does |
|---|---|
| **Create EOS Resources** | Creates the `Resources` folder layout + empty `ModuleKindCatalog` (also runs automatically). |
| **Create Default Bootstrap** | Generator window → `GameBootstrap` MonoBehaviour file. |
| **Create Auto Bootstrap** | Creates/refreshes `Assets/EOS.Generated/EosBootstrap.gen.cs` (`[EosBoot]` orchestrator). |
| **Create World Bootstrap** | Creates/refreshes `Assets/EOS.Generated/WorldBootstrap.gen.cs` (`[EosWorldBootstrap]` provider). |
| **Rebuild Incarnation Index** | Rebuilds `Assets/Resources/incarnations.json` (also automatic on asset changes). |
| **Incarnation Database** | Read-only browser over the index entries + redirects, with Rebuild/Reload. |
| **World Inspector** | Live visualization of the running world (below). |

Asset creation menus: `Sackrany > EOS > Entity Preset`, `Component Set`, `Module Kind Catalog`. Components: `Sackrany/EOS/Entity Preset Spawner`, `Incarnation Pooling`, `Socket Set`.

### World Inspector

A read-only editor window (**Sackrany ▸ EOS ▸ World Inspector**) that polls the core's public API — no core changes, no static hooks. Three tabs:

- **Live** — pick a world, browse/search entities (by id, name, or component), expand a selected entity into its components with live field/property values and tags, and see per-type storage counts.
- **Systems** — the system pipeline as a pannable/zoomable graph. Nodes are systems (colored by update phase, dimmed when disabled, tagged event/reactive), edges are `[UpdateBefore]`/`[UpdateAfter]`, laid out by longest path. The side panel shows the selected system's query signature, group, and order. Works in edit mode too (reflected from code).
- **Groups & Archetypes** — the `SystemGroup` tree with live enable/disable toggles; **data archetypes** (distinct component sets present on live entities, with counts, sample entities, and the systems that match them); and **query archetypes** (systems grouped by their include/exclude/tag signature).

**Cost:** editor-only, stripped from builds; when closed, nothing runs. While open, gathering happens once per ~10 Hz tick (attribute reflection cached per type; only live state re-read) and `OnGUI` just renders the snapshot. Heavy scans (data archetypes) run only when their view is active. **Copy dump** puts `WorldDebug.DumpUniverse()` on the clipboard.

Known approximations: the graph reconstructs execution order from attributes rather than the runner's internal lists, and archetype↔system matching ignores interface params and tag filters.

## Notes & caveats

- **Renaming an incarnation** changes its id (id == path). The postprocessor warns with `old -> new` and a ready-to-paste redirect snippet. Add it to the `Redirects` list in `incarnations.json` to keep old saves resolving; manual redirects are preserved across rebuilds.
- **Pooled views keep state.** The pool deactivates/reactivates instances; it does not reset them. Anything that must not leak between uses (trails, animators, timers) belongs in `IPoolableView.OnRent/OnReturn`.
- **Spawner needs a booted world.** `EntityPresetSpawner.Spawn()` logs an error if `EosLoop.IsBooted` is false — make sure your bootstrap runs first (the generated `GameBootstrap` uses `[DefaultExecutionOrder(-10000)]` for exactly this).
- **Save caveat (your serializer, not the bridge):** components whose serialized `DataType` is a bare numeric (`int`) and that cast `(int)data` directly can hit a boxed-`long` `InvalidCastException` after a JSON round-trip — use a data struct/class or `Convert.ToInt32`. `string` and data classes are fine.
- **Untrusted saves:** `TypeNameHandling.Auto` is a deserialization-attack vector for files from untrusted sources; add a `SerializationBinder` allowlist on your side if saves travel over network/cloud. Local slots are fine.
- **IL2CPP stripping:** `[EosDomainReset]` methods are reflection-invoked — preserve them (see [Domain reset](#domain-reset-eosdomainreset)).
