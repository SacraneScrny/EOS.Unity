#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using EOS.Core;
using EOS.Entities;
using EOS.Objects;
using EOS.Storage;

namespace EOS.Unity.Editor
{
    /// <summary>Read-only polling of the live <see cref="Universe"/> for the World Inspector: worlds, entities, components, fields, tags and storages.</summary>
    internal static class EosIntrospection
    {
        /// <summary>True when a booted, enabled default world is available to inspect.</summary>
        public static bool IsLive => Universe.IsEnabled && Universe.DefaultWorld != null;

        /// <summary>The default world followed by all other live worlds.</summary>
        public static List<IReadOnlyWorld> Worlds()
        {
            var list = new List<IReadOnlyWorld>();
            if (Universe.DefaultWorld != null) list.Add(Universe.DefaultWorld);
            var others = Universe.OtherWorlds;
            if (others != null)
                for (int i = 0; i < others.Count; i++)
                    if (others[i] != null) list.Add(others[i]);
            return list;
        }

        /// <summary>A display label like <c>World #id 'key'</c>.</summary>
        public static string WorldLabel(IReadOnlyWorld world)
        {
            if (world == null) return "<none>";
            var key = string.IsNullOrEmpty(world.Key) ? "" : $" '{world.Key}'";
            return $"World #{world.Id}{key}";
        }

        /// <summary>The number of alive entities in the world.</summary>
        public static int EntityCount(IReadOnlyWorld world)
        {
            if (world == null) return 0;
            int n = 0;
            foreach (var _ in world.Entities.All()) n++;
            return n;
        }

        /// <summary>A snapshot list of all alive entities in the world.</summary>
        public static List<EosEntity> Entities(IReadOnlyWorld world)
        {
            var list = new List<EosEntity>();
            if (world == null) return list;
            foreach (var e in world.Entities.All()) list.Add(e);
            return list;
        }

        /// <summary>The entity's serialization-stable key, or null.</summary>
        public static string StableKey(IReadOnlyWorld world, EosEntity entity)
            => world == null ? null : world.Entities.GetStableKey(entity);

        /// <summary>A read-only snapshot of one component on an entity: its type, name, instance and ready/enabled state.</summary>
        public readonly struct ComponentView
        {
            /// <summary>The component's concrete type.</summary>
            public readonly Type Type;
            /// <summary>The display name of the type.</summary>
            public readonly string Name;
            /// <summary>The live component instance.</summary>
            public readonly object Instance;
            /// <summary>Whether the component is ready and enabled.</summary>
            public readonly bool Ready;

            /// <summary>Captures a component's type, name, instance and ready state.</summary>
            public ComponentView(Type type, string name, object instance, bool ready)
            {
                Type = type;
                Name = name;
                Instance = instance;
                Ready = ready;
            }
        }

        /// <summary>All components on the entity as name-sorted <see cref="ComponentView"/>s.</summary>
        public static List<ComponentView> Components(IReadOnlyWorld world, EosEntity entity)
        {
            var list = new List<ComponentView>();
            if (world == null) return list;
            foreach (var kv in world.ObjectsStorages.AllStorages)
            {
                if (kv.Value is not IIndexedStorage indexed) continue;
                var obj = indexed.TryGetObject(entity);
                if (obj == null) continue;
                bool ready = obj is EosObject eo && eo.IsEnabled;
                list.Add(new ComponentView(kv.Key, NiceName(kv.Key), obj, ready));
            }
            list.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return list;
        }

        /// <summary>The name-sorted component types present on the entity.</summary>
        public static List<Type> ComponentTypes(IReadOnlyWorld world, EosEntity entity)
        {
            var list = new List<Type>();
            if (world == null) return list;
            foreach (var kv in world.ObjectsStorages.AllStorages)
                if (kv.Value is IIndexedStorage indexed && indexed.TryGetObject(entity) != null)
                    list.Add(kv.Key);
            list.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return list;
        }

        /// <summary>A read-only name/value pair for one inspected field or property of a component.</summary>
        public readonly struct FieldView
        {
            /// <summary>The field or property name.</summary>
            public readonly string Name;
            /// <summary>The stringified value.</summary>
            public readonly string Value;
            /// <summary>Captures a field's name and stringified value.</summary>
            public FieldView(string name, string value) { Name = name; Value = value; }
        }

        static readonly Type ObjectBase = typeof(EosObject);

        /// <summary>The public instance fields and readable properties of a component (excluding the <see cref="EosObject"/> base), as name-sorted <see cref="FieldView"/>s.</summary>
        public static List<FieldView> Values(object component)
        {
            var list = new List<FieldView>();
            if (component == null) return list;
            var type = component.GetType();

            foreach (var f in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (f.DeclaringType == ObjectBase) continue;
                list.Add(new FieldView(f.Name, Stringify(SafeGet(() => f.GetValue(component)))));
            }
            foreach (var p in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!p.CanRead || p.GetIndexParameters().Length > 0) continue;
                if (p.DeclaringType == ObjectBase) continue;
                list.Add(new FieldView(p.Name, Stringify(SafeGet(() => p.GetValue(component)))));
            }
            list.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return list;
        }

        static object SafeGet(Func<object> getter)
        {
            try { return getter(); }
            catch (Exception ex) { return "<error: " + (ex.InnerException?.Message ?? ex.Message) + ">"; }
        }

        static string Stringify(object value)
        {
            switch (value)
            {
                case null: return "null";
                case string s: return s;
                case EosEntity e: return $"Entity #{e.Id}";
            }
            try { return value.ToString(); }
            catch { return "<unprintable>"; }
        }

        /// <summary>Fills (and returns) <paramref name="buffer"/> with the entity's tag names.</summary>
        public static List<string> Tags(IReadOnlyWorld world, EosEntity entity, List<string> buffer = null)
        {
            buffer ??= new List<string>();
            if (world == null) { buffer.Clear(); return buffer; }
            world.Tags.GetTagNames(entity, buffer);
            return buffer;
        }

        /// <summary>A read-only snapshot of one component storage: its type, count and reactive high-water marks.</summary>
        public readonly struct StorageView
        {
            /// <summary>The stored component type.</summary>
            public readonly Type Type;
            /// <summary>The display name of the type.</summary>
            public readonly string Name;
            /// <summary>The number of components in the storage.</summary>
            public readonly int Count;
            /// <summary>The storage's max add-version (drives the <c>[New]</c> channel).</summary>
            public readonly ulong MaxAddVersion;
            /// <summary>The storage's max mark-version (drives the <c>[Bumped]</c> channel).</summary>
            public readonly ulong MaxMarkVersion;
            /// <summary>The storage's max enable-version (drives the <c>[Enabled]</c> channel).</summary>
            public readonly ulong MaxEnableVersion;
            /// <summary>The storage's max disable-version (drives the <c>[Disabled]</c> channel).</summary>
            public readonly ulong MaxDisableVersion;
            /// <summary>The storage's max remove-version (drives the <c>[Removed]</c> channel).</summary>
            public readonly ulong MaxRemoveVersion;

            /// <summary>Captures a storage's type, name, count and reactive marks.</summary>
            public StorageView(Type type, string name, int count, ulong add, ulong mark, ulong enable, ulong disable, ulong remove)
            {
                Type = type;
                Name = name;
                Count = count;
                MaxAddVersion = add;
                MaxMarkVersion = mark;
                MaxEnableVersion = enable;
                MaxDisableVersion = disable;
                MaxRemoveVersion = remove;
            }
        }

        /// <summary>All indexed component storages in the world as name-sorted <see cref="StorageView"/>s.</summary>
        public static List<StorageView> Storages(IReadOnlyWorld world)
        {
            var list = new List<StorageView>();
            if (world == null) return list;
            foreach (var kv in world.ObjectsStorages.AllStorages)
            {
                if (kv.Value is not IIndexedStorage indexed) continue;
                list.Add(new StorageView(kv.Key, NiceName(kv.Key), indexed.Count,
                    indexed.MaxAddVersion, indexed.MaxMarkVersion,
                    indexed.MaxEnableVersion, indexed.MaxDisableVersion, indexed.MaxRemoveVersion));
            }
            list.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return list;
        }

        /// <summary>A readable type name with generic arguments expanded (e.g. <c>Incarnation&lt;Transform&gt;</c>).</summary>
        public static string NiceName(Type type)
        {
            if (type == null) return "<null>";
            if (!type.IsGenericType) return type.Name;

            var sb = new StringBuilder();
            var name = type.Name;
            int tick = name.IndexOf('`');
            sb.Append(tick >= 0 ? name.Substring(0, tick) : name);
            sb.Append('<');
            var args = type.GetGenericArguments();
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(NiceName(args[i]));
            }
            sb.Append('>');
            return sb.ToString();
        }
    }
}
#endif
