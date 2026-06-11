using EOS.Attributes;
using EOS.Core;
using EOS.Entities;
using EOS.Extensions;
using EOS.Hierarchy;
using EOS.Systems;
using EOS.Systems.Incarnation;

namespace EOS.Unity
{
    [Group(typeof(IncarnationGroup))]
    public sealed class AssemblyViewBindSystem : EosSystem
    {
        public override UpdateType UpdateType => UpdateType.Update;

        void EventExecute(ParentChanged e)
        {
            if (!e.Child.IsValid) return;
            if (!e.Child.TryGet<AttachedTo>(out var link)) return;
            if (link.Parent == e.NewParent) return;
            if (Services.TryGet<AssemblyService>(out var assemblies))
                assemblies.Detach(e.Child);
        }

        void Execute(AttachedTo link, EosEntity entity)
        {
            if (link.ViewBound) return;
            AssemblyViewBinder.TryBind(entity, link);
        }
    }
}
