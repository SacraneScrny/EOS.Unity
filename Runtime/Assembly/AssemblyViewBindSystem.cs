using EOS.Attributes;
using EOS.Core;
using EOS.Entities;
using EOS.Extensions;
using EOS.Hierarchy;
using EOS.Systems;
using EOS.Systems.Incarnation;

namespace EOS.Unity
{
    /// <summary>Reparents module views under their socket anchors once both endpoint views exist, and detaches the socket link when native reparenting diverges from <see cref="AttachedTo"/>.</summary>
    [Group(typeof(IncarnationGroup))]
    public sealed class AssemblyViewBindSystem : EosSystem
    {
        /// <summary>Runs this system in the Update phase.</summary>
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
