using System;
using EOS.Objects;
using EOS.Objects.Interfaces;
using EOS.Serialization;
using UnityEngine;

namespace EOS.Unity
{
    /// <summary>Component on a module entity that advertises its <see cref="ModuleKind"/>; the kind is validated against a socket at view-bind time.</summary>
    [Serializable]
    public sealed class Module : EosObject, IObjectSerializable, IPoolableObject
    {
        [SerializeField, ModuleKindField] string _kind;

        /// <summary>Clears the stored kind name when the component is disposed (reset for pooled reuse).</summary>
        protected override void OnDispose() => _kind = null;

        /// <summary>The module's kind, interned from its stored name.</summary>
        public ModuleKind Kind
        {
            get => ModuleKind.Of(_kind);
            set => _kind = value.Name;
        }

        /// <summary>The raw stored kind name, set directly without interning.</summary>
        public string KindName
        {
            get => _kind;
            set => _kind = value;
        }

        Type IObjectSerializable.DataType => typeof(string);
        object IObjectSerializable.SerializeData() => _kind;
        void IObjectSerializable.DeserializeData(object data, IDeserializeContext ctx) => _kind = data as string;
    }
}
