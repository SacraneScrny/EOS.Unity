using EOS.Attributes;
using EOS.Core;
using EOS.Entities;
using EOS.Systems;
using EOS.Systems.Incarnation;

namespace EOS.Unity
{
    [Group(typeof(IncarnationGroup))]
    public sealed class AssemblyViewBindSystem : EosSystem
    {
        public override UpdateType UpdateType => UpdateType.Update;

        void Execute(AttachedTo link, EosEntity entity)
        {
            if (link.ViewBound) return;
            AssemblyViewBinder.TryBind(entity, link);
        }
    }
}
