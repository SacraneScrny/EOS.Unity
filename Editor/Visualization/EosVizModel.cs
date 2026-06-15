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
    /// <summary>Builds the World Inspector's cached view model from system/group reflection plus live state: system descriptors, group tree and data archetypes.</summary>
    internal static class EosVizModel
    {
        /// <summary>A reflected descriptor of one system: phase, ordering edges, query shape and computed layout layer.</summary>
        public sealed class SystemInfo
        {
            /// <summary>The system's concrete type.</summary>
            public Type Type;
            /// <summary>The system type name.</summary>
            public string Name;
            /// <summary>The phase the system runs in.</summary>
            public UpdateType Phase;
            /// <summary>Whether the system is currently enabled (live worlds only).</summary>
            public bool Enabled = true;
            /// <summary>Whether the system declares an <c>EventExecute</c>.</summary>
            public bool IsEvent;
            /// <summary>Whether the system has a reactive (<c>[New]</c>/<c>[Bumped]</c>/<c>[Enabled]</c>/<c>[Disabled]</c>/<c>[Removed]</c>) parameter.</summary>
            public bool Reactive;

            /// <summary>The assigned <c>[Group]</c> type, or null.</summary>
            public Type Group;
            /// <summary>Types this system runs after (<c>[UpdateAfter]</c>).</summary>
            public readonly List<Type> After = new();
            /// <summary>Types this system runs before (<c>[UpdateBefore]</c>).</summary>
            public readonly List<Type> Before = new();
            /// <summary>The tie-break order value.</summary>
            public int Order;

            /// <summary>Mandatory concrete component parameters and <c>[Include]</c> filters.</summary>
            public readonly List<Type> Include = new();
            /// <summary><c>[Optional]</c> component parameters.</summary>
            public readonly List<Type> Optional = new();
            /// <summary><c>[Exclude]</c> filter types.</summary>
            public readonly List<Type> Exclude = new();
            /// <summary>Tag filters, prefixed by kind.</summary>
            public readonly List<string> Tags = new();

            /// <summary>The topological layer used for graph layout.</summary>
            public int Layer;

            /// <summary>A stable identifier (full type name).</summary>
            public string Id => Type.FullName ?? Type.Name;

            /// <summary>A compact textual signature of the query (includes, excludes and tags).</summary>
            public string QuerySignature()
            {
                var inc = Include.Count == 0 ? "*" : string.Join(", ", Include.Select(EosIntrospection.NiceName));
                var exc = Exclude.Count > 0 ? "  !" + string.Join(", !", Exclude.Select(EosIntrospection.NiceName)) : "";
                var tag = Tags.Count > 0 ? "  #" + string.Join(" #", Tags) : "";
                return inc + exc + tag;
            }
        }

        static readonly Dictionary<Type, SystemInfo> _structCache = new();
        static List<SystemInfo> _reusable;

        /// <summary>Descriptors for every system in the live world (or all reflected systems when none), with layout layers computed.</summary>
        public static List<SystemInfo> Systems(IReadOnlyWorld world)
        {
            var infos = _reusable ??= new List<SystemInfo>();
            infos.Clear();

            if (world != null)
            {
                foreach (var system in world.Systems.All)
                {
                    var info = StructFor(system.GetType());
                    info.Enabled = system.IsEnabled;
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
            catch { }
            return UpdateType.Update;
        }

        /// <summary>All concrete <see cref="EosSystem"/> subtypes across loaded assemblies.</summary>
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

        static void ComputeLayers(List<SystemInfo> infos)
        {
            var byType = new Dictionary<Type, SystemInfo>();
            foreach (var i in infos) byType[i.Type] = i;

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

        /// <summary>A node in the system-group tree: its type, parent/children, contained systems and enabled state.</summary>
        public sealed class GroupNode
        {
            /// <summary>The group type, or null for the synthetic root.</summary>
            public Type Type;
            /// <summary>The group name.</summary>
            public string Name;
            /// <summary>The parent group node.</summary>
            public GroupNode Parent;
            /// <summary>Child group nodes.</summary>
            public readonly List<GroupNode> Children = new();
            /// <summary>Systems assigned directly to this group.</summary>
            public readonly List<SystemInfo> Systems = new();
            /// <summary>Whether the group is enabled (live worlds only).</summary>
            public bool Enabled = true;

            /// <summary>A stable identifier for the node.</summary>
            public string Id => Type?.FullName ?? Type?.Name ?? "<ungrouped>";
        }

        /// <summary>Builds the nested group tree, attaching each system to its group node.</summary>
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

        /// <summary>All concrete <see cref="SystemGroup"/> subtypes across loaded assemblies.</summary>
        public static IEnumerable<Type> SystemGroupTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeTypes)
                .Where(t => t != null && t.IsSubclassOf(typeof(SystemGroup)) && !t.IsAbstract);
        }

        /// <summary>A distinct component-set observed among live entities: its component types, entity count and a sample of members.</summary>
        public sealed class Archetype
        {
            /// <summary>The component types defining this archetype.</summary>
            public readonly List<Type> Components = new();
            /// <summary>How many entities share this component-set.</summary>
            public int Count;
            /// <summary>A capped sample of member entities.</summary>
            public readonly List<EosEntity> Sample = new();
            /// <summary>A readable label listing the components.</summary>
            public string Label;
        }

        const int SampleCap = 32;

        /// <summary>Groups live entities by their distinct component-sets, ordered by descending count.</summary>
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

        /// <summary>Whether the archetype satisfies the system's concrete include/exclude filters (approximate: ignores interfaces and tags).</summary>
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
