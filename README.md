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
- No external packages required. The incarnation index uses `JsonUtility`
  (no Newtonsoft dependency in this package).

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
