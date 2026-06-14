using System;
using System.Collections.Generic;
using EOS.Entities;
using EOS.Objects;
using EOS.Objects.Interfaces;
using EOS.Serialization;

namespace EOS.Unity
{
    /// <summary>Root component holding the authoritative socketId to child-module map for an assembly; cascade-destroys held modules on dispose.</summary>
    [Serializable]
    public sealed class EntityAssembly : EosObject, IObjectSerializable, IPoolableObject
    {
        Dictionary<string, EosEntity> _modules;
        Dictionary<string, EosEntity> Map => _modules ??= new Dictionary<string, EosEntity>();

        /// <summary>True when no valid module currently occupies the given socket.</summary>
        public bool IsFree(string socketId) => !TryGetModule(socketId, out _);

        /// <summary>Resolves the module in the given socket; prunes stale entries and returns false when empty or invalid.</summary>
        public bool TryGetModule(string socketId, out EosEntity module)
        {
            module = EosEntity.Null;
            if (_modules == null || string.IsNullOrEmpty(socketId)) return false;
            if (!_modules.TryGetValue(socketId, out var e)) return false;
            if (!e.IsValid)
            {
                _modules.Remove(socketId);
                return false;
            }

            module = e;
            return true;
        }

        /// <summary>Appends all currently held valid modules to the list and returns how many were added.</summary>
        public int Collect(List<EosEntity> into)
        {
            if (into == null || _modules == null) return 0;

            int count = 0;
            foreach (var pair in _modules)
            {
                if (!pair.Value.IsValid) continue;
                into.Add(pair.Value);
                count++;
            }

            return count;
        }

        internal void Hold(string socketId, EosEntity module) => Map[socketId] = module;

        internal void ReleaseIfHolds(string socketId, EosEntity module)
        {
            if (_modules != null && _modules.TryGetValue(socketId, out var e) && e == module)
                _modules.Remove(socketId);
        }

        /// <summary>Cascade-destroys every held module when the assembly is disposed.</summary>
        protected override void OnDispose()
        {
            if (_modules == null || _modules.Count == 0) return;

            var snapshot = new List<EosEntity>(_modules.Values);
            _modules.Clear();

            for (int i = 0; i < snapshot.Count; i++)
                if (snapshot[i].IsValid)
                    snapshot[i].Destroy();

            _modules.Clear();
        }

        Type IObjectSerializable.DataType => typeof(AssemblyData);

        object IObjectSerializable.SerializeData()
        {
            var data = new AssemblyData();
            if (_modules != null)
            {
                foreach (var pair in _modules)
                    if (pair.Value.IsValid)
                        data.Links.Add(new AssemblyLink { SocketId = pair.Key, ChildLocalId = pair.Value.Id });
            }

            return data;
        }

        void IObjectSerializable.DeserializeData(object data, IDeserializeContext ctx)
        {
            if (!(data is AssemblyData d)) return;

            Map.Clear();
            for (int i = 0; i < d.Links.Count; i++)
            {
                var link = d.Links[i];
                var child = ctx.Resolve(link.ChildLocalId);
                if (child.IsValid) Map[link.SocketId] = child;
            }
        }
    }

    /// <summary>Serialization payload for an <see cref="EntityAssembly"/>: the list of socket-to-child links.</summary>
    [Serializable]
    public sealed class AssemblyData
    {
        /// <summary>The serialized socket-to-child links.</summary>
        public List<AssemblyLink> Links = new();
    }

    /// <summary>One serialized socket link: the socket id and the local id of the child module in the snapshot.</summary>
    [Serializable]
    public sealed class AssemblyLink
    {
        /// <summary>The socket the module occupies.</summary>
        public string SocketId;
        /// <summary>The snapshot-local id of the child module entity.</summary>
        public int ChildLocalId;
    }
}
