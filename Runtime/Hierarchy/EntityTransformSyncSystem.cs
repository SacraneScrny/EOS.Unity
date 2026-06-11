using EOS.Attributes;
using EOS.Core;
using EOS.Entities;
using EOS.Extensions;
using EOS.Systems;
using EOS.Systems.Incarnation;
using UnityEngine;

namespace EOS.Unity
{
    [Group(typeof(IncarnationGroup))]
    [UpdateOrder(UpdateOrderPhase.AfterAll)]
    [UpdateAfter(typeof(HierarchyViewSystem))]
    [UpdateBefore(typeof(IncarnationSyncSystem))]
    public sealed class EntityTransformSyncSystem : EosSystem
    {
        public override UpdateType UpdateType => UpdateType.Update;

        void Execute(EntityTransform transform, EosEntity entity)
        {
            var view = AssemblyViewBinder.GetViewObject(entity);
            if (view == null) return;

            if (entity.TryGet<AttachedTo>(out var link))
            {
                if (link.ViewBound && view.transform.parent != null)
                    ApplyLocal(view.transform, transform.LocalPosition, transform.LocalRotation, transform.LocalScale);
                return;
            }

            var anchorEntity = HierarchyViewSystem.FindViewAncestor(entity, out var anchorView);
            if (anchorView == null)
            {
                EntityTransform.GetWorldTrs(entity, out var worldPosition, out var worldRotation, out var worldScale);
                ApplyWorld(view.transform, worldPosition, worldRotation, worldScale);
                return;
            }

            if (view.transform.parent != anchorView.transform) return;

            EntityTransform.GetTrsRelativeTo(entity, anchorEntity, out var position, out var rotation, out var scale);
            ApplyLocal(view.transform, position, rotation, scale);
        }

        static void ApplyLocal(Transform target, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (target.localPosition != position) target.localPosition = position;
            if (target.localRotation != rotation) target.localRotation = rotation;
            if (target.localScale != scale) target.localScale = scale;
        }

        static void ApplyWorld(Transform target, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (target.position != position) target.position = position;
            if (target.rotation != rotation) target.rotation = rotation;
            if (target.localScale != scale) target.localScale = scale;
        }
    }
}
