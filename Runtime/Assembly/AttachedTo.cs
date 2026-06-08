using System;
using EOS.Entities;
using EOS.Extensions;
using EOS.Objects;
using EOS.Serialization;
using UnityEngine;

namespace EOS.Unity
{
    [Serializable]
    public sealed class AttachedTo : EosObject, IObjectSerializable
    {
        public EosEntity Parent { get; internal set; }
        public string SocketId { get; internal set; }

        public Vector3 LocalPosition;
        public Quaternion LocalRotation = Quaternion.identity;

        internal bool ViewBound;

        protected override void OnDispose()
        {
            if (Parent.IsValid && Parent.TryGet<EntityAssembly>(out var asm))
                asm.ReleaseIfHolds(SocketId, Entity);
        }

        Type IObjectSerializable.DataType => typeof(AttachedToData);

        object IObjectSerializable.SerializeData() => new AttachedToData
        {
            ParentLocalId = Parent.Id,
            SocketId = SocketId,
            LocalPosition = LocalPosition,
            LocalRotation = LocalRotation,
        };

        void IObjectSerializable.DeserializeData(object data, IDeserializeContext ctx)
        {
            if (!(data is AttachedToData d)) return;

            Parent = ctx.Resolve(d.ParentLocalId);
            SocketId = d.SocketId;
            LocalPosition = d.LocalPosition;
            LocalRotation = d.LocalRotation;
            ViewBound = false;
        }
    }

    [Serializable]
    public sealed class AttachedToData
    {
        public int ParentLocalId;
        public string SocketId;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
    }
}
