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
    /// <summary>Child-side link component recording which parent assembly and socket a module is attached to; removed on detach.</summary>
    [Serializable]
    public sealed class AttachedTo : EosObject, IObjectSerializable, IPoolableObject
    {
        /// <summary>The parent assembly entity this module is attached to.</summary>
        public EosEntity Parent { get; internal set; }
        /// <summary>The socket on the parent this module occupies.</summary>
        public string SocketId { get; internal set; }

        internal bool ViewBound;
        internal bool Detaching;

        /// <summary>Releases the parent socket link and unbinds the view on dispose, unless an in-progress detach already handled it.</summary>
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

    /// <summary>Serialization payload for an <see cref="AttachedTo"/>: the parent's local id and socket; legacy offset fields migrate into <see cref="EntityTransform"/> on load.</summary>
    [Serializable]
    public sealed class AttachedToData
    {
        /// <summary>The snapshot-local id of the parent assembly entity.</summary>
        public int ParentLocalId;
        /// <summary>The socket the module occupies on the parent.</summary>
        public string SocketId;
    }
}
