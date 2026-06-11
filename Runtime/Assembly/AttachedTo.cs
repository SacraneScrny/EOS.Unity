using System;
using EOS.Entities;
using EOS.Extensions;
using EOS.Logging;
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
        internal bool Detaching;

        protected override void OnDispose()
        {
            if (Detaching) return;

            if (Parent.IsValid && Parent.TryGet<EntityAssembly>(out var asm))
                asm.ReleaseIfHolds(SocketId, Entity);

            AssemblyViewBinder.Unbind(Entity);

            try
            {
                if (Services.TryGet<AssemblyService>(out var assemblies))
                    assemblies.NotifyDetachedOnDispose(Parent, Entity, SocketId);
            }
            catch (Exception ex)
            {
                EosLog.Error($"detach notification threw: {ex}", nameof(AttachedTo));
            }
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
