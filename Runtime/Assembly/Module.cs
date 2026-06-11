using System;
using EOS.Objects;
using EOS.Objects.Interfaces;
using EOS.Serialization;
using UnityEngine;

namespace EOS.Unity
{
    [Serializable]
    public sealed class Module : EosObject, IObjectSerializable, IPoolableObject
    {
        [SerializeField, ModuleKindField] string _kind;

        protected override void OnDispose() => _kind = null;

        public ModuleKind Kind
        {
            get => ModuleKind.Of(_kind);
            set => _kind = value.Name;
        }

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
