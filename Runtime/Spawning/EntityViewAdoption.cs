using System.Collections.Generic;
using EOS.Entities;

namespace EOS.Unity
{
    /// <summary>
    /// Bridges the prefab-first spawn path to the standard incarnation flow: <see cref="EntityActorSpawner"/> offers a
    /// pre-spawned view here, and <see cref="EntityIncarnationBinder"/> adopts it (instead of instantiating a fresh
    /// prefab) when the entity's <c>Incarnation</c> awakes. Entries are consumed within a frame.
    /// </summary>
    internal static class EntityViewAdoption
    {
        static readonly Dictionary<EosEntity, EntityIncarnation> _pending = new();

        public static void Offer(EosEntity entity, EntityIncarnation view)
        {
            if (!entity.IsValid || view == null) return;
            _pending[entity] = view;
        }

        public static bool TryAdopt(EosEntity entity, out EntityIncarnation view)
        {
            if (_pending.TryGetValue(entity, out view))
            {
                _pending.Remove(entity);
                return true;
            }
            view = null;
            return false;
        }

        public static void Clear() => _pending.Clear();

        [EosDomainReset]
        static void OnDomainReset() => Clear();
    }
}
