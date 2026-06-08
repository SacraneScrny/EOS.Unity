using System;
using System.Collections.Generic;
using UnityEngine;

namespace EOS.Unity
{
    public readonly struct ModuleKind : IEquatable<ModuleKind>
    {
        public static readonly ModuleKind None = default;

        public readonly int Id;

        ModuleKind(int id) => Id = id;

        public string Name => ModuleKindRegistry.NameOf(Id);
        public bool IsValid => Id > 0;

        public static ModuleKind Of(string name)
            => string.IsNullOrEmpty(name) ? None : new ModuleKind(ModuleKindRegistry.Intern(name));

        public static ModuleKind Of<TEnum>(TEnum value) where TEnum : struct, Enum
            => Of(value.ToString());

        public bool Equals(ModuleKind other) => Id == other.Id;
        public override bool Equals(object obj) => obj is ModuleKind k && k.Id == Id;
        public override int GetHashCode() => Id;
        public override string ToString() => Name ?? "<none>";

        public static bool operator ==(ModuleKind a, ModuleKind b) => a.Id == b.Id;
        public static bool operator !=(ModuleKind a, ModuleKind b) => a.Id != b.Id;
    }

    internal static class ModuleKindRegistry
    {
        static readonly Dictionary<string, int> _ids = new();
        static readonly List<string> _names = new() { null };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            _ids.Clear();
            _names.Clear();
            _names.Add(null);
        }

        public static int Intern(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0;
            if (_ids.TryGetValue(name, out var id)) return id;

            id = _names.Count;
            _names.Add(name);
            _ids[name] = id;
            return id;
        }

        public static string NameOf(int id)
            => id > 0 && id < _names.Count ? _names[id] : null;
    }
}
