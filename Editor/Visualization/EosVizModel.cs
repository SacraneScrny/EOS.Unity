#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using EOS.Attributes;
using EOS.CodeGen;
using EOS.Core;
using EOS.Entities;
using EOS.Systems;
using EOS.Systems.Groups;

namespace EOS.Unity.Editor
{
    /// <summary>
    /// Static structural model derived from the EOS core: systems (with their phase,
    /// update-order edges, query signature), the <see cref="SystemGroup"/> tree, and the
    /// live data archetypes. Built by reflection over the public attribute/type surface,
    /// so it needs no changes to the core.
    /// </summary>
    internal static class EosVizModel
    {
        // ---- Systems -----------------------------------------------------------------

        public sealed class SystemInfo
        {
            public Type Type;
            public string Name;
            public UpdateType Phase;
            public bool Enabled = true;
            public bool IsEvent;
            public bool Reactive;

            public Type Group;                       // [Group(typeof(...))]
            public readonly List<Type> After = new(); // [UpdateAfter]
            public readonly List<Type> Before = new();// [UpdateBefore]
            public int Order;                         // [UpdateOrder]

            public readonly List<Type> Include = new();
            public readonly List<Type> Optional = new();
            public readonly List<Type> Exclude = new();
            public readonly List<string> Tags = new();

            public int Layer;  // computed for layout

            public string Id => Type.FullName ?? Type.Name;

            public string QuerySignature()
            {
                var inc = Include.Count == 0 ? "*" : string.Join(", ", Include.Select(EosIntrospection.NiceName));
                var exc = Exclude.Count > 0 ? "  !" + string.Join(", !", Exclude.Select(EosIntrospection.NiceName)) : "";
                var tag = Tags.Count > 0 ? "  #" + string.Join(" #", Tags) : "";
                return inc + exc + tag;
            }
        }

        // Structural reflection is code-only and never changes at runtime, so it is reflected
        // once per type and cached for the lifetime of the domain (cleared on assembly reload).
        // Only the live bits (Enabled/Phase) are refreshed per sample, with no reflection.
        static readonly Dictionary<Type, SystemInfo> _structCache = new();
        static List<SystemInfo> _reusable;

        /// <summary>
        /// All systems. When the universe is live we read enabled/phase from the running
        /// instances; otherwise we reflect every <see cref="EosSystem"/> subclass in the
        /// domain (best-effort phase via a throwaway instance). The expensive attribute
        /// reflection is cached; repeated calls only re-read live state and re-layer.
        /// </summary>
        public static List<SystemInfo> Systems(IReadOnlyWorld world)
        {
            var infos = _reusable ??= new List<SystemInfo>();
            infos.Clear();

            if (world != null)
            {
                foreach (var system in world.Systems.All)
                {
                    var info = StructFor(system.GetType());
                    info.Enabled = system.IsEnabled;  // live, no reflection
                    info.Phase = system.UpdateType;
                    infos.Add(info);
                }
            }
            else
            {
                foreach (var type in EosSystemTypes())
                    infos.Add(StructFor(type));
            }

            ComputeLayers(infos);
            return infos;
        }

        /// <summary>Cached structural description of one system type (phase is best-effort until live).</summary>
        static SystemInfo StructFor(Type type)
        {
            if (_structCache.TryGetValue(type, out var cached)) return cached;
            var info = Describe(type);
            _structCache[type] = info;
            return info;
        }

        static SystemInfo Describe(Type type)
        {
            var info = new SystemInfo { Type = type, Name = type.Name };
            info.Phase = TryReadPhase(type);

            var group = type.GetCustomAttribute<GroupAttribute>();
            if (group != null) info.Group = group.Group;

            foreach (var a in type.GetCustomAttributes<UpdateAfterAttribute>()) info.After.Add(a.Target);
            foreach (var a in type.GetCustomAttributes<UpdateBeforeAttribute>()) info.Before.Add(a.Target);
            var order = type.GetCustomAttribute<UpdateOrderAttribute>();
            if (order != null) info.Order = order.Order;

            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.Name == "Execute" || m.Name == "EventExecute");

            foreach (var method in methods)
            {
                if (method.Name == "EventExecute") info.IsEvent = true;
                if (SystemShape.IsReactive(method)) info.Reactive = true;

                foreach (var p in SystemShape.Parameters(method))
                {
                    if (!p.IsConcrete) continue;
                    if (p.Optional) AddUnique(info.Optional, p.Type);
                    else AddUnique(info.Include, p.Type);
                }
                foreach (var a in method.GetCustomAttributes<IncludeAttribute>(true))
                    foreach (var t in a.Types) AddUnique(info.Include, t);
                foreach (var a in method.GetCustomAttributes<ExcludeAttribute>(true))
                    foreach (var t in a.Types) AddUnique(info.Exclude, t);
                foreach (var a in method.GetCustomAttributes<TagFilterAttribute>(true))
                    AddTags(info.Tags, a);
            }

            return info;
        }

        static void AddUnique(List<Type> list, Type type)
        {
            if (type != null && !list.Contains(type)) list.Add(type);
        }

        static void AddTags(List<string> tags, TagFilterAttribute attr)
        {
            string prefix = attr switch
            {
                WithoutTagAttribute => "!",
                WithAnyTagAttribute => "any:",
                WithOneTagAttribute => "one:",
                _ => "",
            };
            if (attr.Tags == null) return;
            foreach (var t in attr.Tags)
                if (t != null) tags.Add(prefix + t);
        }

        static UpdateType TryReadPhase(Type type)
        {
            try
            {
                if (Activator.CreateInstance(type) is EosSystem s) return s.UpdateType;
            }
            catch { /* not constructable without a world — fall through */ }
            return UpdateType.Update;
        }

        public static IEnumerable<Type> EosSystemTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeTypes)
                .Where(t => t != null && t.IsSubclassOf(typeof(EosSystem)) && !t.IsAbstract);
        }

        static IEnumerable<Type> SafeTypes(Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null); }
            catch { return Array.Empty<Type>(); }
        }

        /// <summary>Longest-path layering over UpdateBefore/After edges, per phase, for left-to-right layout.</summary>
        static void ComputeLayers(List<SystemInfo> infos)
        {
            var byType = new Dictionary<Type, SystemInfo>();
            foreach (var i in infos) byType[i.Type] = i;

            // Build successor edges: After(target) => this depends on target (target -> this);
            // Before(target) => this -> target.
            var succ = new Dictionary<SystemInfo, List<SystemInfo>>();
            var indeg = new Dictionary<SystemInfo, int>();
            foreach (var i in infos) { succ[i] = new List<SystemInfo>(); indeg[i] = 0; }

            void Link(SystemInfo from, SystemInfo to)
            {
                if (from == null || to == null || from == to) return;
                if (from.Phase != to.Phase) return;
                succ[from].Add(to);
                indeg[to]++;
            }

            foreach (var i in infos)
            {
                foreach (var t in i.After) if (byType.TryGetValue(t, out var dep)) Link(dep, i);
                foreach (var t in i.Before) if (byType.TryGetValue(t, out var dep)) Link(i, dep);
            }

            // Kahn longest-path layering (cycles are tolerated: leftovers stay at layer 0).
            var queue = new Queue<SystemInfo>(infos.Where(i => indeg[i] == 0));
            foreach (var i in infos) i.Layer = 0;
            var remaining = new Dictionary<SystemInfo, int>(indeg);
            while (queue.Count > 0)
            {
                var n = queue.Dequeue();
                foreach (var m in succ[n])
                {
                    if (m.Layer < n.Layer + 1) m.Layer = n.Layer + 1;
                    if (--remaining[m] == 0) queue.Enqueue(m);
                }
            }
        }

        // ---- System group tree -------------------------------------------------------

        public sealed class GroupNode
        {
            public Type Type;
            public string Name;
            public GroupNode Parent;
            public readonly List<GroupNode> Children = new();
            public readonly List<SystemInfo> Systems = new();
            public bool Enabled = true;

            public string Id => Type?.FullName ?? Type?.Name ?? "<ungrouped>";
        }

        /// <summary>
        /// Builds the <see cref="SystemGroup"/> hierarchy (parent = base type) and attaches
        /// each system to its <c>[Group]</c>. Systems without a group go under a synthetic root.
        /// </summary>
        public static GroupNode GroupTree(IReadOnlyWorld world, List<SystemInfo> systems)
        {
            var root = new GroupNode { Type = null, Name = "(systems)" };
            var nodes = new Dictionary<Type, GroupNode>();

            GroupNode NodeFor(Type t)
            {
                if (t == null) return root;
                if (nodes.TryGetValue(t, out var n)) return n;
                n = new GroupNode { Type = t, Name = t.Name };
                nodes[t] = n;
                if (world != null) n.Enabled = SafeIsEnabled(world, t);

                var parentType = ParentGroup(t);
                var parent = parentType != null ? NodeFor(parentType) : root;
                n.Parent = parent;
                parent.Children.Add(n);
                return n;
            }

            // Seed every known SystemGroup type so empty groups still show up.
            foreach (var t in SystemGroupTypes()) NodeFor(t);

            foreach (var s in systems)
                NodeFor(s.Group).Systems.Add(s);

            return root;
        }

        static bool SafeIsEnabled(IReadOnlyWorld world, Type groupType)
        {
            try { return world.SystemGroups.IsEnabled(groupType); }
            catch { return true; }
        }

        static Type ParentGroup(Type groupType)
        {
            var baseType = groupType.BaseType;
            if (baseType != null && baseType != typeof(SystemGroup) && typeof(SystemGroup).IsAssignableFrom(baseType))
                return baseType;
            return null;
        }

        public static IEnumerable<Type> SystemGroupTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeTypes)
                .Where(t => t != null && t.IsSubclassOf(typeof(SystemGroup)) && !t.IsAbstract);
        }

        // ---- Data archetypes ---------------------------------------------------------

        public sealed class Archetype
        {
            public readonly List<Type> Components = new();
            public int Count;
            public readonly List<EosEntity> Sample = new(); // capped
            public string Label;
        }

        const int SampleCap = 32;

        /// <summary>Distinct component-sets actually present on live entities, with counts.</summary>
        public static List<Archetype> DataArchetypes(IReadOnlyWorld world)
        {
            var map = new Dictionary<string, Archetype>();
            if (world == null) return new List<Archetype>();

            foreach (var entity in world.Entities.All())
            {
                var types = EosIntrospection.ComponentTypes(world, entity);
                string key = types.Count == 0
                    ? "(empty)"
                    : string.Join("|", types.Select(t => t.FullName));

                if (!map.TryGetValue(key, out var arch))
                {
                    arch = new Archetype();
                    arch.Components.AddRange(types);
                    arch.Label = types.Count == 0
                        ? "(no components)"
                        : string.Join(", ", types.Select(EosIntrospection.NiceName));
                    map[key] = arch;
                }
                arch.Count++;
                if (arch.Sample.Count < SampleCap) arch.Sample.Add(entity);
            }

            var list = map.Values.ToList();
            list.Sort((a, b) => b.Count.CompareTo(a.Count));
            return list;
        }

        /// <summary>
        /// Approximate match between a data archetype and a system query: mandatory includes
        /// must be a subset of the archetype and excludes must not intersect. Interface
        /// params and tag filters are not evaluated here (noted in the UI).
        /// </summary>
        public static bool Matches(SystemInfo system, Archetype archetype)
        {
            var present = new HashSet<Type>(archetype.Components);
            foreach (var inc in system.Include)
                if (!present.Contains(inc)) return false;
            foreach (var exc in system.Exclude)
                if (present.Contains(exc)) return false;
            return true;
        }
    }
}
#endif
