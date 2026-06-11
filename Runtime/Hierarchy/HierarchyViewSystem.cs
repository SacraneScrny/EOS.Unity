using EOS.Attributes;
using EOS.Core;
using EOS.Entities;
using EOS.Extensions;
using EOS.Hierarchy;
using EOS.Objects;
using EOS.Systems;
using EOS.Systems.Incarnation;
using UnityEngine;

namespace EOS.Unity
{
    [Group(typeof(IncarnationGroup))]
    [UpdateOrder(UpdateOrderPhase.AfterAll)]
    [UpdateBefore(typeof(IncarnationSyncSystem))]
    public sealed class HierarchyViewSystem : EosSystem
    {
        public override UpdateType UpdateType => UpdateType.Update;

        [Exclude(typeof(AttachedTo))]
        void Execute([Each] IIncarnation inc, EosEntity entity)
        {
            if (!entity.HasParent()) return;

            var view = AssemblyViewBinder.GetViewObject(entity);
            if (view == null) return;

            FindViewAncestor(entity, out var anchorView);
            if (anchorView == null) return;
            if (view.transform.parent == anchorView.transform) return;

            view.transform.SetParent(anchorView.transform, true);
        }

        void EventExecute(ParentChanged e)
        {
            if (e.NewParent.IsValid) return;
            if (!e.Child.IsValid || e.Child.Has<AttachedTo>()) return;

            var view = AssemblyViewBinder.GetViewObject(e.Child);
            if (view == null) return;

            var oldView = e.OldParent.IsValid ? AssemblyViewBinder.GetViewObject(e.OldParent) : null;
            var current = view.transform.parent;
            if (oldView != null && current != null && current.IsChildOf(oldView.transform))
                view.transform.SetParent(null, true);
        }

        internal static EosEntity FindViewAncestor(EosEntity entity, out GameObject anchorView)
        {
            var parent = entity.GetParent();
            while (parent.IsValid)
            {
                anchorView = AssemblyViewBinder.GetViewObject(parent);
                if (anchorView != null) return parent;
                parent = parent.GetParent();
            }

            anchorView = null;
            return EosEntity.Null;
        }
    }
}
