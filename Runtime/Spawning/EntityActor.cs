using EOS.Entities;
using EOS.Extensions;

namespace EOS.Unity
{
    /// <summary>
    /// Base MonoBehaviour for the lazy, reflection-free prefab-first spawn path: a pooled prefab that becomes its
    /// entity's view. Put it on a prefab (add <see cref="IncarnationPooling"/> to pool it) and spawn instances with
    /// <see cref="EntityActorSpawner"/>. Override <see cref="OnBuild"/> to add components on spawn and
    /// <see cref="OnRent"/>/<see cref="OnReturn"/> to reset pooled state; the per-phase <c>On*</c> sync hooks are
    /// inherited from <see cref="EntityIncarnation"/>.
    /// </summary>
    public abstract class EntityActor : EntityIncarnation, IPoolableView
    {
        internal void InvokeBuild(EosEntity entity) => OnBuild(entity);

        /// <summary>Called once during <see cref="EntityActorSpawner"/> spawn, before the entity is activated, to add and configure components on <paramref name="entity"/>; never runs on serialization restore.</summary>
        protected virtual void OnBuild(EosEntity entity) { }

        /// <summary>Called when this instance is rented from the pool and reactivated; override to restore visual state for reuse.</summary>
        public virtual void OnRent() { }

        /// <summary>Called when this instance is returned to the pool and deactivated; override to clear transient visual state.</summary>
        public virtual void OnReturn() { }

        /// <summary>Destroys the bound entity, which despawns this view back to its pool; a safe no-op when already unbound.</summary>
        public void Despawn()
        {
            if (Entity.IsValid) Entity.Destroy();
        }
    }
}
