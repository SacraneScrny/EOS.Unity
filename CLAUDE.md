# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repository is

EOS.Unity is the Unity consumer assembly for the engine-free EOS ECS core (a sibling repository). It drives `Universe` from the PlayerLoop, implements the incarnation (view) seam with prefab binders and pooling, adds ScriptableObject authoring (presets, component sets), a runtime entity-assembly (socket/module) layer, attribute-driven bootstrap codegen, and editor tooling. **Never modify the core from here** — attach only through its public seams: `Universe`, `IncarnationBridge`, `WorldBootstrap.Provider`, `EosLog.OnLog`, `EosProfiler.Backend`, `WorldSerializer`/`WorldLoader`, `world.ServiceRegistry`.

## Build & tooling

- Unity package by folder convention, **no asmdef on purpose**: `Runtime/` compiles into `Assembly-CSharp`, `Editor/` into `Assembly-CSharp-Editor`. The core ships no asmdef either, so everything shares `Assembly-CSharp` and `internal` core members are visible here (e.g. presets use `Universe.DefaultWorld` internals via same-assembly access). A standalone asmdef would not compile against `Assembly-CSharp` types — if the core ever gets an asmdef, this package gets one in the same pass.
- No test runner, no build commands. Validate by careful reading; compile errors surface in the consuming Unity project.
- Editor-only code must stay under `Editor/` (or `#if UNITY_EDITOR`). Runtime code may not reference `UnityEditor`.
- External dependency: **MackySoft.SubclassSelector** (preset inspectors' `[SerializeReference]` pickers). The incarnation index uses `JsonUtility` only.
- `.gitignore` excludes `*.meta` — this repo is dropped into a Unity project that generates its own metas.

## Code style

- Follow the core's style: 4-space indentation, no aligning tabs, expression-bodied members where natural.
- Comments are rare. XML doc summaries exist on the public boot attributes and some editor visualization code; do not add narrative inline comments elsewhere. The core itself allows **no comments at all**.
- Error handling: `try/catch` + `EosLog.Error/Warning` with `nameof(TheClass)` as source — never swallow silently, never crash a boot step or a pool notification because user code threw.
- Editor windows/menus live under `Sackrany ▸ EOS ▸ ...`; asset menus under `Sackrany/EOS/...`. Keep new tooling consistent.

## Architecture

### Boot pipeline (Runtime root, `Runtime/Boot/`)

Two stages, automatic vs explicit:

1. **`EosRuntimeBootstrap`** — `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`, every play session: `EosDomainReset.Reset()` (core statics) → `UnityLogHandler.Install()` → `EosDomainResetRunner.Run()` (user `[EosDomainReset]` methods) → register default binders (`GameObjectBinder`, `EntityIncarnationBinder`) → hook `Application.quitting` → `EosLoop.Shutdown()`. `EosEditorTeardown` (editor) also shuts down on exiting Play Mode — required for Enter-Play-Mode-Options without domain reload.
2. **`EosLoop.Boot(EosBootConfig config = null)`** — explicit, idempotent (`IsBooted` guard). Order: `UnityLogHandler.MinLevel = config.MinLogLevel` → profiler backend (`UnityProfilerBackend` if `EnableProfiler`, else null backend) → `IncarnationDatabase.Load()` → `config.ApplyBinders()` (exceptions caught per binder) → `Universe.Boot()` → `EosPlayerLoop.Install()` → `EosDebugDrawer.Ensure()` if `DebugDraw`. `Shutdown()` reverses: uninstall loop, remove drawer, `Universe.Shutdown()`, `ViewPoolRegistry.ClearAll()`, `IncarnationDatabase.Unload()`.

`EosPlayerLoop` inserts marker structs (`EosFixedUpdate`/`EosUpdate`/`EosLateUpdate`) at the **start** of PlayerLoop stages `FixedUpdate`, `Update`, `PreLateUpdate`, each guarded by `Universe.IsEnabled`. Install removes stale own nodes first (idempotent).

`EosBootConfig` (`[Serializable]`): `EnableProfiler` (false), `DebugDraw` (true), `MinLogLevel` (Debug), `AddBinder<TView>(binder)` chainable — binders are deferred and applied during Boot.

### Attribute boot & codegen (`Runtime/Boot/` + `Editor/Boot/`)

Two generated files under `Assets/EOS.Generated/`, both **opt-in by existence**: a menu item creates them; while present, `[DidReloadScripts]` regenerates on every recompile (writes only when content changed); deleting the file disables the feature. Invalid method shapes are skipped with a warning so generated files always compile.

- **`EosBootstrap.gen.cs`** (menu *Create Auto Bootstrap*, `EosBootCodegen`): collects `[EosBoot]` (`public static void()`) and `[EosBootConfigProvider]` (`public static EosBootConfig(EosBootConfig)`) methods on public non-generic types. Generated `Run()` is `public`, runs at `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)`. **Cold path**: providers thread one config (sorted), `EosLoop.Boot(config)`, then all steps. **Warm path** (already booted): only `isFallback: true` steps. Ordering: Kahn topological sort over `[EosBootBefore/After(type, method = null)]` edges, then `Order`, then type/method name; cycles logged and demoted to `Order`. Each step is individually try/caught.
- **`WorldBootstrap.gen.cs`** (menu *Create World Bootstrap*, `EosWorldBootstrapCodegen`): collects `[EosWorldBootstrap]` (`public static void(World)`), sorted by `Order` then name (no Before/After DAG). Generated `Install()` runs at `SubsystemRegistration` and sets `EOS.Loader.WorldBootstrap.Provider = Register` — so every `World.Init()` **and** `World.Reset()` re-runs the collected methods for that world. This is the per-world seeding path; per-session setup belongs in `[EosBoot]`.
- **`EosGeneratedSystems.gen.cs`** (menu *Create System Registry*, `EosSystemRegistryCodegen`): the editor driver for the core's `SystemRegistryGenerator` — calls its `public BuildSource(namespace, className)` and writes the result with the same opt-in/write-if-changed dance. The emitted registry self-installs into `GeneratedSystems.Provider` via `[ModuleInitializer]`, switching `SystemsRunner` from reflection to the zero-alloc generated bodies. While the file exists it is kept in sync on every recompile, so the registry never goes stale after adding/removing/changing systems; deleting it reverts to reflection.

`[EosDomainReset]` (`static void()`, any visibility) is the third lifecycle attribute — **reflection-discovered** by `EosDomainResetRunner` (assembly scan skipping System./Unity./mscorlib/netstandard/Mono., delegate cache per domain, re-invoked each `Run()`), no ordering. IL2CPP can strip these — docs tell users to `[Preserve]` them.

### Incarnations (`Runtime/Incarnation/`)

- `IncarnationIndex` / `IncarnationDatabase`: `Resources/incarnations.json` (`ResourceKey = "incarnations"`) maps id → prefab path; id == path under `Resources/Incarnations/` without extension. `Resolve(id)` follows the `Redirects` list (rename support) with a 64-hop cycle guard and caches prefabs by resolved id. `Load()` is lazy; `Unload()` on shutdown.
- `GameObjectBinder` (`IIncarnationBinder<GameObject>`): spawn-and-forget, empty syncs.
- `EntityIncarnationBinder` (`IIncarnationBinder<EntityIncarnation>`): instantiates, sets `view.Entity`, calls `InvokeBind`; `Destroy` calls `InvokeUnbind`, clears `Entity`, despawns. Sync phases dispatch to `OnSync/OnSyncFixed/OnSyncLate`. All user hooks try/caught.
- `EntityIncarnation` (abstract MonoBehaviour): `Entity` property + protected virtuals `OnBind/OnUnbind/OnSync/OnSyncFixed/OnSyncLate`, invoked via internal `Invoke*` wrappers.
- **Both binders spawn through `ViewPoolRegistry`** — pooling is transparent at the binder level.

### View pooling (`Runtime/Pooling/`)

Opt-in **per prefab** via the `IncarnationPooling` component (`Preload` ≥ 0, `MaxSize` ≥ 1, default 32). `ViewPoolRegistry.Spawn(prefab)` checks for it: pooled → `GameObjectPool.Rent()`, else plain `Instantiate`. `Despawn(instance)` tries `TryReturn` (via the auto-added `PooledView` marker holding its owner pool) and destroys on failure/overflow. Pools park deactivated instances under a hidden `"EOS View Pool"` root (`HideAndDontSave`). `Rent` unparents, activates, then notifies; `Return` notifies, deactivates, reparents. `IPoolableView.OnRent/OnReturn` fire on **all** implementing components in children (recursive, try/caught). The registry is `[EosDomainReset]`-cleared and cleared on `EosLoop.Shutdown()`. The pool does **not** reset instance state — that's the view's job in `OnRent/OnReturn`.

### Presets (`Runtime/Presets/` + `Editor/Presets/`)

- `EntityPreset` (ScriptableObject, `Sackrany/EOS/Entity Preset`): name/active/serializable flags, incarnation kind (`IncarnationViewKind.None/EntityIncarnation/GameObject`) + id, tags, `[SerializeReference]` component templates, referenced `EntityComponentSet`s + per-type `_setOverrides`, and `_defaultModules` (assembly defaults). `Instantiate(world?)` order: create entity **inactive** (`new EosEntity(world, name, false, serializable)`) → ApplySets (overrides win by type) → ApplyTags → ApplyIncarnation (`entity.Add<Incarnation<TView>>().Setup(id)`) → ApplyComponents → `entity.On()` if active → `AssemblyDefaults.Apply(...)`. On exception: log, destroy the partial entity, return `EosEntity.Null`. Component addition resolves storage by type (`world.ObjectsStorages.GetOrCreate`), adds a fresh component, then `EosCloneUtility.CopyDeclaredFields(template, component)`.
- `EosCloneUtility`: **Unity-serialization-aligned deep copy** — copies exactly the fields Unity would serialize (public or `[SerializeField]`/`[SerializeReference]`; not static/literal/readonly/`[NonSerialized]`), declared down to the `EosObject` boundary. Deep-clones arrays, generic `List<>`, `[Serializable]` classes with parameterless ctors, and non-plain structs; passes primitives/enums/strings/`UnityEngine.Object` refs through; refuses delegates and `System.Collections.*`; depth-capped at 32 with a warning. Field-serialization and plain-struct checks are cached per type.
- `EntityComponentSet` (ScriptableObject, `Sackrany/EOS/Component Set`): tags + component templates + optional `[SerializeReference]` `ComponentSetModule` (code module). `Collect()` returns a `ComponentSetBuilder` populated from list + module (`Build(builder)`, try/caught). One component per type per entity — sets apply in order, preset extras after; value changes to a set component go through preset-side overrides.
- `EntityPresetSpawner` (MonoBehaviour): spawns on `Start` if `_spawnOnStart`; **requires `EosLoop.IsBooted`** (errors otherwise — it does not boot); `LastSpawned`, optional `_destroyAfterSpawn`.
- Editor: `EntityPresetEditor` / `EntityComponentSetEditor` (Info/Data foldouts, per-asset persisted state, Override/Revert/Delete block actions, incarnation-id dropdown from the index, default-module socket dropdown read from the prefab's `SocketSet`, play-mode Spawn button), `ComponentPickerDropdown` (namespace-tree `AdvancedDropdown`), `PresetEditorUtility` (shared foldout/block/list/tag drawing + concrete-type discovery).

### Entity assemblies (`Runtime/Assembly/` + `Editor/Assembly/`)

Spec: `EOS.UnityAssembly.md` (Phases 1–2 implemented; the pooling phase landed as `Runtime/Pooling/`). A typed, serializable parent→socket→child graph between entities — the one primitive the flat ECS core lacks.

- `ModuleKind` (readonly struct): interned int id + `Name`; `Of(string)`, `Of<TEnum>`, `None`; **serialized by name** (save-stable). `ModuleKindRegistry` is the interner. `ModuleKindCatalog` (ScriptableObject) + `[ModuleKindField]` + `ModuleKindFieldDrawer` give a dropdown over all catalogs (2 s cache, free-text fallback).
- `SocketSet` MonoBehaviour on the **incarnation prefab** root, `Socket { Id, Kind, Anchor }`, gizmos. Resolved at runtime through the entity's incarnation view.
- Components (all `IObjectSerializable`): `EntityAssembly` (root; socketId→child map; `IsFree/TryGetModule/Collect`; **cascade-destroys** held modules in `OnDispose`), `Module` (`Kind`), `AttachedTo` (child; `Parent`, `SocketId`, `LocalPosition/LocalRotation` per-attachment offset; runtime-only `ViewBound`/`Detaching` flags; `OnDispose` releases the parent link).
- `AssemblyService` (one per world, registered via `[EosWorldBootstrap]` in `AssemblyServiceBootstrap`; `world.Assemblies()` lazily creates one if codegen is absent): `Attach(parent, socketId, module[, pos, rot])`, `Detach(module)`, `SetLocalOffset`, `TryGetModule`, `GetModules(parent, into)`, `IsSocketFree`. Validation: parent has `EntityAssembly` → parent view has `SocketSet` with the id → socket free → module's `Kind` matches. **Immediate when not iterating; deferred onto `world.AfterCurrentPhase` (re-validated) when iterating** — extension methods pick automatically. Emits `ModuleAttached`/`ModuleDetached` EOS events.
- `AssemblyViewBindSystem` (`[Group(typeof(IncarnationGroup))]`): per `AttachedTo` with `!ViewBound`, `AssemblyViewBinder.TryBind` — waits until both endpoint views exist, validates kind (detaches on mismatch), reparents the child view under the anchor with the stored offset, sets `ViewBound`. Self-healing per link → load order and nesting depth don't matter.
- `DefaultModule { SocketId, EntityPreset }` + `AssemblyDefaults.Apply`: spawn-and-attach inside `EntityPreset.Instantiate` only (never on snapshot restore — "new only" semantics), depth guard 32, orphans destroyed if attach fails.
- Save/load is entirely `WorldSerializer` — both link directions serialize (the offset lives child-side only); a reconciler repairs a missing back-link but never overrides the offset.

### Logging / profiling / debug draw

- `UnityLogHandler`: `EosLog.OnLog` → Unity console as `[EOS:source] msg`; `MinLevel` filter before formatting; Error→`LogError`, Warning→`LogWarning`, else `Log`.
- `UnityProfilerBackend`: label→`ProfilerMarker` cache + stack for balanced nesting; `End()` on empty stack is a safe no-op.
- `EosDebugDrawer`: hidden `HideAndDontSave` GameObject whose `OnDrawGizmos` calls `Universe.DebugDraw()` — play mode only; `Ensure()`/`Remove()` idempotent.

### Editor infrastructure (`Editor/`)

- `EosFolderBootstrap` (`[InitializeOnLoad]` + menu *Create EOS Resources*): creates `Assets/Resources{,/Incarnations,/EntityPresets}` and an empty `ModuleKindCatalog.asset` (only if none exists anywhere); touches the filesystem only when missing.
- `IncarnationIndexBuilder` (AssetPostprocessor + menu *Rebuild Incarnation Index*): rebuilds `incarnations.json` from `.prefab`s under `Resources/Incarnations/`; warns with a ready-to-paste redirect snippet on renames; **preserves manual `Redirects` across rebuilds**.
- `IncarnationDatabaseWindow`: read-only entries/redirects browser with Rebuild/Reload.
- `BootstrapGeneratorWindow` (menu *Create Default Bootstrap*): writes the `GameBootstrap` MonoBehaviour template (`[DefaultExecutionOrder(-10000)]`, `IsBooted` guard, serialized `EosBootConfig`); never overwrites.
- `Visualization/` — **World Inspector** (menu *World Inspector*): `EosInspectorWindow` (tabs Live / Systems / Groups & Archetypes), `EosGraphView` (pan/zoom/drag IMGUI canvas), `EosVizModel` (cached attribute reflection + live state per ~10 Hz `OnInspectorUpdate` tick), `EosIntrospection` (read-only world polling). Zero overhead when closed; no static hooks; archetype↔system matching ignores interface params and tag filters (approximate by design).

## Gotchas

- Generated files (`EosBootstrap.gen.cs`, `WorldBootstrap.gen.cs`) regenerate **only if they already exist**; creating them is always an explicit menu action. Don't auto-create them from code.
- `AssemblyService` ops from inside systems **must** stay deferred — they are structural changes; the core's `StructuralChangePolicy` throws otherwise.
- Anything spawned through binders goes via `ViewPoolRegistry` — when changing binder code, keep `Spawn`/`Despawn` symmetric or pooled prefabs will leak or double-destroy.
- Pooled instances are not reset by the pool; state reset belongs in `IPoolableView.OnRent/OnReturn`.
- Preset cloning must stay Unity-serialization-aligned (`EosCloneUtility`) — if a field wouldn't survive Unity serialization on the asset, it must not be copied at spawn either.
- `[EosDomainReset]` methods are reflection-invoked → IL2CPP stripping hazard; generated-code-invoked attributes (`[EosBoot]`, `[EosWorldBootstrap]`) are safe.
- `EntityPresetSpawner` does not boot EOS; the bootstrap must run first (`[DefaultExecutionOrder(-10000)]` on the generated `GameBootstrap`).
- Incarnation ids are paths — renames need a redirect entry in `incarnations.json` for old saves to resolve.
