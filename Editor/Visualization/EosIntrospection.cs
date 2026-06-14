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
    internal static class EosIntrospection
    {
        public static bool IsLive => Universe.IsEnabled && Universe.DefaultWorld != null;

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

        public static string WorldLabel(IReadOnlyWorld world)
        {
            if (world == null) return "<none>";
            var key = string.IsNullOrEmpty(world.Key) ? "" : $" '{world.Key}'";
            return $"World #{world.Id}{key}";
        }

        public static int EntityCount(IReadOnlyWorld world)
        {
            if (world == null) return 0;
            int n = 0;
            foreach (var _ in world.Entities.All()) n++;
            return n;
        }

        public static List<EosEntity> Entities(IReadOnlyWorld world)
        {
            var list = new List<EosEntity>();
            if (world == null) return list;
            foreach (var e in world.Entities.All()) list.Add(e);
            return list;
        }

        public static string StableKey(IReadOnlyWorld world, EosEntity entity)
            => world == null ? null : world.Entities.GetStableKey(entity);

        public readonly struct ComponentView
        {
            public readonly Type Type;
            public readonly string Name;
            public readonly object Instance;
            public readonly bool Ready;

            public ComponentView(Type type, string name, object instance, bool ready)
            {
                Type = type;
                Name = name;
                Instance = instance;
                Ready = ready;
            }
        }

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

        public readonly struct FieldView
        {
            public readonly string Name;
            public readonly string Value;
            public FieldView(string name, string value) { Name = name; Value = value; }
        }

        static readonly Type ObjectBase = typeof(EosObject);

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

        public static List<string> Tags(IReadOnlyWorld world, EosEntity entity, List<string> buffer = null)
        {
            buffer ??= new List<string>();
            if (world == null) { buffer.Clear(); return buffer; }
            world.Tags.GetTagNames(entity, buffer);
            return buffer;
        }

        public readonly struct StorageView
        {
            public readonly Type Type;
            public readonly string Name;
            public readonly int Count;
            public readonly ulong MaxAddVersion;
            public readonly ulong MaxMarkVersion;

            public StorageView(Type type, string name, int count, ulong add, ulong mark)
            {
                Type = type;
                Name = name;
                Count = count;
                MaxAddVersion = add;
                MaxMarkVersion = mark;
            }
        }

        public static List<StorageView> Storages(IReadOnlyWorld world)
        {
            var list = new List<StorageView>();
            if (world == null) return list;
            foreach (var kv in world.ObjectsStorages.AllStorages)
            {
                if (kv.Value is not IIndexedStorage indexed) continue;
                list.Add(new StorageView(kv.Key, NiceName(kv.Key), indexed.Count,
                    indexed.MaxAddVersion, indexed.MaxMarkVersion));
            }
            list.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return list;
        }

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
