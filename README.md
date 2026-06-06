# EOS.Unity — Unity bridge for the EOS ECS

Engine-integration layer for the EOS core. The core stays engine-free; this
package is the consumer assembly that drives it from Unity's PlayerLoop, plugs
the view (incarnation) layer into prefabs, and wires logging/profiling/saves.

The core is **not modified** by this package — it only attaches through the
existing seams (`Universe`, `IncarnationBridge`, `EosLog`, `EosProfiler`,
`WorldSerializer`).

## Requirements

- The EOS core sources present in the same Unity project.
- Unity with the `UnityEngine.LowLevel` PlayerLoop API (2019.3+).
- **MackySoft.SubclassSelector** — required for the entity preset inspector
  (`[SerializeReference]` component picker). Install via UPM:
  `https://github.com/mackysoft/Unity-SerializeReferenceExtensions.git?path=Assets/MackySoft/MackySoft.SerializeReferenceExtensions`
  (Package Manager → Add package from git URL).
- The incarnation index uses `JsonUtility` (no Newtonsoft dependency).

## Install

Drop the `EOS.Unity` folder anywhere under `Assets/`. Layout:

```
EOS.Unity/
  Runtime/    -> compiles into Assembly-CSharp (no asmdef)
  Editor/     -> compiles into Assembly-CSharp-Editor (Unity 'Editor' folder)
```

No asmdef is shipped on purpose: the core has none, so everything lands in
`Assembly-CSharp` and sees each other. If you later move the core into an
asmdef, add one here too and reference the core's asmdef.

## Folder convention

Incarnation prefabs live under a dedicated Resources folder — this is the
"obvious separation" of incarnation content from the rest of the project:

```
Assets/Resources/Incarnations/...      <- your incarnation prefabs
Assets/Resources/incarnations.json     <- generated index (do not edit by hand)
```

The index is rebuilt automatically by an `AssetPostprocessor` whenever anything
under `Resources/Incarnations` changes, and via
**Sackrany ▸ EOS ▸ Rebuild Incarnation Index**. View it read-only via
**Sackrany ▸ EOS ▸ Incarnation Database**.

The `id` of an incarnation is its path under `Incarnations/` without extension
(`Resources/Incarnations/Enemies/Orc.prefab` → id `Enemies/Orc`). This id is
what you pass to `Setup(...)` and what gets serialized into saves.

## Boot

Static setup (Unity log handler + default binders + domain reset) runs
automatically on `SubsystemRegistration`. You start the world explicitly:

```csharp
using EOS.Unity;

public static class Game
{
    public static void Start()
    {
        var config = new EosBootConfig
        {
            EnableProfiler = false,           // true -> ProfilerMarker backend
            DebugDraw = true,                 // hidden Gizmos drawer (editor only)
            MinLogLevel = EOS.Logging.LogLevel.Debug,
        };

        // register custom view binders (defaults for GameObject and
        // EntityIncarnation are already registered):
        // config.AddBinder(new MyShipBinder());

        EosLoop.Boot(config);
    }
}
```

`EosLoop.Boot` loads the incarnation index, applies custom binders, boots the
`Universe`, and injects three nodes into the PlayerLoop:

- `Universe.FixedUpdate(Time.fixedDeltaTime)` → PlayerLoop `FixedUpdate`
- `Universe.Update(Time.deltaTime)` → start of PlayerLoop `Update`
- `Universe.LateUpdate(Time.deltaTime)` → PlayerLoop `PreLateUpdate`

Injection is idempotent (own nodes are removed before re-inserting), and they
are torn down on `Application.quitting` and on exiting Play Mode in the editor.
Pause is free: `timeScale = 0` yields `dt = 0` and stops Unity's FixedUpdate.

## Incarnations (views)

Two binders are registered by default:

- `GameObject` — spawn-and-forget visual, no sync logic.
- `EntityIncarnation` — a `MonoBehaviour` base with lifecycle hooks; the view
  pulls its data straight from ECS.

Author a view by subclassing `EntityIncarnation` on the prefab root:

```csharp
using EOS.Unity;
using EOS.Extensions;
using UnityEngine;

public sealed class OrcView : EntityIncarnation
{
    protected override void OnBind() { /* spawned, Entity is set */ }

    protected override void OnSync()
    {
        if (Entity.TryGet<Position>(out var p))
            transform.position = new Vector3(p.X, p.Y, p.Z);
    }

    protected override void OnUnbind() { /* about to be destroyed */ }
}
```

Attach a view to an entity through the closed-generic incarnation component:

```csharp
using EOS.Objects;

var inc = entity.Add<Incarnation<EntityIncarnation>>();
inc.Setup("Enemies/Orc");   // id from the index
entity.On();                // activate -> Awake instantiates the prefab
```

For a logic-less visual, use `Incarnation<GameObject>` instead.

## Entity presets (ScriptableObject)

Instead of hand-writing the create-entity / add-components / configure / set
incarnation / activate dance for every kind of object, author it once as a
**preset** asset and spawn it anywhere.

Create one via `Assets > Create > Sackrany > EOS > Entity Preset`. The inspector
holds everything needed to boot an entity:

- **Name / Active / Serializable** — the `EosEntity` flags.
- **Incarnation** — pick the view kind (`EntityIncarnation`, `GameObject`, or
  `None`) and an id. The id field has a dropdown sourced from
  `incarnations.json`, so you select from real prefabs.
- **Tags** — a plain string list.
- **Components** — a `[SerializeReference]` list of `EosObject` subclasses,
  configured inline. Use **Add Component** for a type picker over every concrete
  component in the project; field values you set on the asset are the spawn
  defaults.

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

…or drop an **Entity Preset Spawner** component (`Sackrany/EOS/Entity Preset
Spawner`) on a GameObject: assign the preset, and it spawns on `Start`
(booting EOS first if needed). There's also a **Spawn Into Default World** button
on the preset inspector while in Play Mode.

How configured data reaches the live component: the preset stores each component
as a serialized template; at spawn the matching `Storage<T>` is resolved by type
(`ObjectsStorages.GetOrCreate`), a fresh component is added, and the template's
fields are **deep-copied** onto it — entities never share reference-type fields
(`List<>`, nested classes) with the asset or with each other. The entity is
created inactive, fully configured, then activated, so `Awake`/`Start` (and the
incarnation's view instantiation) see the final data.

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

The preset inspector is split into two foldout blocks — **Info** (name, flags,
incarnation) and **Data** (tags, sets, components) — whose expanded state is
remembered per asset.

Components are edited as individual blocks, not a raw list:

- **Add Component** opens a searchable type-picker window.
- The block's foldout is titled by the component type and is expanded by default;
  its open/closed state is remembered per asset.
- A component's **type is fixed after adding** — there is no type dropdown on an
  existing element. To change type, delete and re-add.
- Each block has a **✕** (delete, with confirmation) and **Revert** (reset to
  default values, with confirmation). The list itself can't be reordered or
  resized directly. Tags work the same way (add/remove via buttons).

**MackySoft.SubclassSelector** is still required (it backs the
`[SerializeReference]` component fields).

### Component sets (required, shared archetypes)

Some entity types share a fixed set of components that must always be present —
miss one and a system's query silently skips the entity, which is a nasty bug to
track down. A **Component Set** is a separate asset that bundles such a set, and
any number of presets reference it.

Create one via `Assets > Create > Sackrany > EOS > Component Set` — it holds the
same kind of `[SerializeReference]` component list (plus tags) as a preset. Then,
on any `EntityPreset`, add the set under **Component Sets ▸ Sets**.

What you get:

- **Always applied.** At spawn, every referenced set's components (and tags) are
  added to the entity, before the preset's own extras.
- **Can't be dropped by accident.** Set components show under *"Set Components
  (required)"* in the preset inspector as read-only — there is no per-component
  delete. To remove them you remove the *whole* set from the list.
- **Synced.** Edit the set asset once and every preset that references it picks up
  the change (structure and values).
- **Locally overridable.** Press **Override** on a set component to materialize a
  local, editable copy stored in the preset; its values win for that preset only.
  **Revert** drops the override and re-syncs to the set. Overrides are keyed by
  component type (one component per type per entity, same as `Storage<T>`), and
  orphaned overrides are pruned automatically when a set or component goes away.
- **Composable.** A preset can reference several sets (e.g. `Damageable` +
  `Movable`); they're merged, first set wins on a type clash, and the preset's own
  `Components` list is applied last on top.

Spawning is unchanged — `preset.Instantiate()` resolves sets, overrides, extras,
tags and incarnation into one entity.

#### Code modules

Sometimes a set is easier to express in code than to author as data. Rather than
subclassing the ScriptableObject (single inheritance, one asset per behaviour),
a Component Set has an optional **code module**: a plain `[Serializable]` class
that builds into the set. The set asset stays one sealed type — drop it into a
preset's **Sets** field as usual; the module just adds to what it contributes.

Write a module by deriving from `ComponentSetModule`:

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

In the Component Set inspector, the **Code Module** slot has a **Set Module**
button (searchable type picker over every `ComponentSetModule`); once assigned it
shows the module's `[SerializeField]` fields, with **Change** / **clear**.

Builder API: `Add<T>()` (returns the new template), `Add<T>(instance)` /
`Add(instance)`, `AddTag(stringOrEnum)`. A set runs its serialized
components/tags **and** its module, then everything flows through the same merge,
override and de-dupe path. In a preset's inspector, module-built components appear
under *"Set Components (required)"* by type with an **Override** button (their
values live in code, so press Override to edit them locally).

## Saves

This package adds no save logic. The EOS snapshot plugs into your
SackranySerializable `DataManager` in your own bootstrap, in two lines.
Nothing else is required — your `JsonSettings` already use
`TypeNameHandling.Auto` + `AssemblyFormatHandling.Simple`, so the snapshot
round-trips version-tolerantly with no custom converters.

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

- **Logging:** `EosLog` is mirrored to the Unity console as `[EOS:source] msg`
  (`Error`→`LogError`, `Warning`→`LogWarning`, `Debug`→`Log`). `EosLog.Debug`
  is `[Conditional("DEBUG")]`, so it is stripped from non-development builds.
  Filter with `EosBootConfig.MinLogLevel`.
- **Profiling:** off by default (zero overhead). With `EnableProfiler = true`,
  a `ProfilerMarker`-based backend is installed (cached markers, balanced
  begin/end), visible in the Unity Profiler.
- **Debug draw:** a hidden (`HideAndDontSave`) MonoBehaviour forwards
  `OnDrawGizmos` to `Universe.DebugDraw()`. Editor Scene/Game view only.

## World Inspector (editor visualization)

A read-only editor window visualizes the running world. Open it via
**Sackrany ▸ EOS ▸ World Inspector**. It polls the core's public API (no core
changes) and has three tabs:

- **Live** — pick a world, browse entities, expand a selected entity into its
  components with live field/property values and tags, and see per-type storage
  counts. Search by id, name, or component.
- **Systems** — the system pipeline as a pannable/zoomable IMGUI graph. Nodes
  are systems (colored by update phase, dimmed when disabled, tagged
  event/reactive), edges are `[UpdateBefore]`/`[UpdateAfter]`, laid out by
  longest-path layer. The side panel shows the selected system's query
  signature, group, and order. Works in edit mode too (reflected from code).
- **Groups & Archetypes** — three sub-views: the `SystemGroup` tree with live
  enable/disable toggles (via `SystemGroups.SetEnabled`); **data archetypes**
  (distinct component-sets present on live entities, with counts, sample
  entities, and the systems that match them); and **query archetypes** (systems
  grouped by their include/exclude/tag signature).

Live data refreshes a few times per second while in Play mode. **Copy dump**
puts `WorldDebug.DumpUniverse()` on the clipboard.

**Cost:** the window is editor-only and stripped from builds, and has no static
hooks — when it is closed, nothing runs (zero overhead). While open, all
expensive gathering happens once per ~10 Hz tick (system reflection is cached
per type; only live state is re-read), and `OnGUI` just renders that snapshot,
so per-event repaints stay cheap. Heavy scans (e.g. data archetypes) run only
when their view is the active one.

Limitations without core changes: individual systems can't be toggled
(`EosSystem.IsEnabled` has no public setter — only whole groups can); the graph
reconstructs execution order from attributes rather than the runner's internal
lists; and archetype↔system matching ignores interface params and tag filters.

## Notes

- **Renaming an incarnation** changes its id (id == path). The postprocessor
  warns with `old -> new` and a ready-to-paste redirect snippet. Add it to the
  `Redirects` list in `incarnations.json` to keep old saves resolving; manual
  redirects are preserved across rebuilds.
- **Save caveat (your side, not the bridge):** components whose serialized
  `DataType` is a bare numeric (`int`) and that cast `(int)data` directly can
  hit a boxed-`long` `InvalidCastException` after a JSON round-trip — use a data
  struct/class or `Convert.ToInt32`. `string` and data classes are fine.
- **Untrusted saves:** `TypeNameHandling.Auto` is a deserialization-attack
  vector for files from untrusted sources; add a `SerializationBinder` allowlist
  on your side if saves travel over network/cloud. Local slots are fine.
