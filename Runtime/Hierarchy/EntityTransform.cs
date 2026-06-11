using System;
using EOS.Entities;
using EOS.Extensions;
using EOS.Objects;
using EOS.Objects.Interfaces;
using EOS.Serialization;
using UnityEngine;

namespace EOS.Unity
{
    [Serializable]
    public sealed class EntityTransform : EosObject, IObjectSerializable, IPoolableObject
    {
        public Vector3 LocalPosition = Vector3.zero;
        public Quaternion LocalRotation = Quaternion.identity;
        public Vector3 LocalScale = Vector3.one;

        public Vector3 WorldPosition
        {
            get
            {
                GetParentWorldTrs(Entity, out var position, out var rotation, out var scale);
                return position + rotation * Vector3.Scale(scale, LocalPosition);
            }
            set
            {
                GetParentWorldTrs(Entity, out var position, out var rotation, out var scale);
                var local = Quaternion.Inverse(rotation) * (value - position);
                LocalPosition = new Vector3(
                    scale.x != 0f ? local.x / scale.x : 0f,
                    scale.y != 0f ? local.y / scale.y : 0f,
                    scale.z != 0f ? local.z / scale.z : 0f);
            }
        }

        public Quaternion WorldRotation
        {
            get
            {
                GetParentWorldTrs(Entity, out _, out var rotation, out _);
                return rotation * LocalRotation;
            }
            set
            {
                GetParentWorldTrs(Entity, out _, out var rotation, out _);
                LocalRotation = Quaternion.Inverse(rotation) * value;
            }
        }

        public Vector3 LossyScale
        {
            get
            {
                GetParentWorldTrs(Entity, out _, out _, out var scale);
                return Vector3.Scale(scale, LocalScale);
            }
        }

        public static void GetWorldTrs(EosEntity entity, out Vector3 position, out Quaternion rotation, out Vector3 scale)
        {
            GetParentWorldTrs(entity, out position, out rotation, out scale);
            if (entity.IsValid && entity.TryGet<EntityTransform>(out var t))
                Compose(ref position, ref rotation, ref scale, t);
        }

        public static void GetTrsRelativeTo(EosEntity entity, EosEntity ancestor, out Vector3 position, out Quaternion rotation, out Vector3 scale)
        {
            if (!entity.IsValid || entity == ancestor)
            {
                position = Vector3.zero;
                rotation = Quaternion.identity;
                scale = Vector3.one;
                return;
            }

            var parent = entity.GetParent();
            if (parent.IsValid && parent != ancestor)
            {
                GetTrsRelativeTo(parent, ancestor, out position, out rotation, out scale);
            }
            else
            {
                position = Vector3.zero;
                rotation = Quaternion.identity;
                scale = Vector3.one;
            }

            if (entity.TryGet<EntityTransform>(out var t))
                Compose(ref position, ref rotation, ref scale, t);
        }

        static void GetParentWorldTrs(EosEntity entity, out Vector3 position, out Quaternion rotation, out Vector3 scale)
        {
            var parent = entity.IsValid ? entity.GetParent() : EosEntity.Null;
            if (parent.IsValid)
            {
                GetWorldTrs(parent, out position, out rotation, out scale);
            }
            else
            {
                position = Vector3.zero;
                rotation = Quaternion.identity;
                scale = Vector3.one;
            }
        }

        static void Compose(ref Vector3 position, ref Quaternion rotation, ref Vector3 scale, EntityTransform t)
        {
            position += rotation * Vector3.Scale(scale, t.LocalPosition);
            rotation *= t.LocalRotation;
            scale = Vector3.Scale(scale, t.LocalScale);
        }

        protected override void OnDispose()
        {
            LocalPosition = Vector3.zero;
            LocalRotation = Quaternion.identity;
            LocalScale = Vector3.one;
        }

        Type IObjectSerializable.DataType => typeof(EntityTransformData);

        object IObjectSerializable.SerializeData() => new EntityTransformData
        {
            LocalPosition = LocalPosition,
            LocalRotation = LocalRotation,
            LocalScale = LocalScale,
        };

        void IObjectSerializable.DeserializeData(object data, IDeserializeContext ctx)
        {
            if (!(data is EntityTransformData d)) return;

            LocalPosition = d.LocalPosition;
            LocalRotation = d.LocalRotation;
            LocalScale = d.LocalScale;
        }
    }

    [Serializable]
    public sealed class EntityTransformData
    {
        public Vector3 LocalPosition = Vector3.zero;
        public Quaternion LocalRotation = Quaternion.identity;
        public Vector3 LocalScale = Vector3.one;
    }
}
