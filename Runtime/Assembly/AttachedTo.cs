using System;
using EOS.Entities;
using EOS.Extensions;
using EOS.Logging;
using EOS.Objects;
using EOS.Objects.Interfaces;
using EOS.Serialization;
using UnityEngine;

namespace EOS.Unity
{
    [Serializable]
    public sealed class AttachedTo : EosObject, IObjectSerializable, IPoolableObject
    {
        public EosEntity Parent { get; internal set; }
        public string SocketId { get; internal set; }

        internal bool ViewBound;
        internal bool Detaching;

        protected override void OnDispose()
        {
            if (!Detaching)
            {
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

            Parent = EosEntity.Null;
            SocketId = null;
            ViewBound = false;
            Detaching = false;
        }

        Type IObjectSerializable.DataType => typeof(AttachedToData);

        object IObjectSerializable.SerializeData() => new AttachedToData
        {
            ParentLocalId = Parent.Id,
            SocketId = SocketId,
        };

        void IObjectSerializable.DeserializeData(object data, IDeserializeContext ctx)
        {
            if (!(data is AttachedToData d)) return;

            Parent = ctx.Resolve(d.ParentLocalId);
            SocketId = d.SocketId;
            ViewBound = false;
        }
    }

    [Serializable]
    public sealed class AttachedToData
    {
        public int ParentLocalId;
        public string SocketId;
    }
}
