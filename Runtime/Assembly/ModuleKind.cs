using System;
using System.Collections.Generic;
using UnityEngine;

namespace EOS.Unity
{
    /// <summary>Interned, save-stable module type tag matched against socket kinds; created via <see cref="Of(string)"/> and serialized by <see cref="Name"/>.</summary>
    public readonly struct ModuleKind : IEquatable<ModuleKind>
    {
        /// <summary>The empty kind (id 0); <see cref="IsValid"/> is false and it matches no named socket kind.</summary>
        public static readonly ModuleKind None = default;

        /// <summary>The interned integer id; 0 means <see cref="None"/>.</summary>
        public readonly int Id;

        ModuleKind(int id) => Id = id;

        /// <summary>The interned name, or null for <see cref="None"/>.</summary>
        public string Name => ModuleKindRegistry.NameOf(Id);
        /// <summary>True for any named kind (id greater than 0), false for <see cref="None"/>.</summary>
        public bool IsValid => Id > 0;

        /// <summary>Interns the given name into a kind; null or empty yields <see cref="None"/>.</summary>
        public static ModuleKind Of(string name)
            => string.IsNullOrEmpty(name) ? None : new ModuleKind(ModuleKindRegistry.Intern(name));

        /// <summary>Interns an enum value (by its name) into a kind.</summary>
        public static ModuleKind Of<TEnum>(TEnum value) where TEnum : struct, Enum
            => Of(value.ToString());

        /// <summary>True when both kinds share the same interned id.</summary>
        public bool Equals(ModuleKind other) => Id == other.Id;
        /// <summary>True when the object is a <see cref="ModuleKind"/> with the same id.</summary>
        public override bool Equals(object obj) => obj is ModuleKind k && k.Id == Id;
        /// <summary>Returns the interned id as the hash code.</summary>
        public override int GetHashCode() => Id;
        /// <summary>Returns the kind name, or "&lt;none&gt;" for <see cref="None"/>.</summary>
        public override string ToString() => Name ?? "<none>";

        /// <summary>Equality by interned id.</summary>
        public static bool operator ==(ModuleKind a, ModuleKind b) => a.Id == b.Id;
        /// <summary>Inequality by interned id.</summary>
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
