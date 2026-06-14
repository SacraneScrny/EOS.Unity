using EOS.Entities;
using UnityEngine;

namespace EOS.Unity
{
    /// <summary>
    /// Base MonoBehaviour for prefab views bound to an entity by <see cref="EntityIncarnationBinder"/>. Override the
    /// <c>On*</c> hooks to react to bind/unbind and per-phase sync.
    /// </summary>
    public abstract class EntityIncarnation : MonoBehaviour
    {
        /// <summary>The entity this view is currently bound to, or <see cref="EosEntity.Null"/> when unbound.</summary>
        public EosEntity Entity { get; internal set; }

        internal void InvokeBind() => OnBind();
        internal void InvokeUnbind() => OnUnbind();
        internal void InvokeSync() => OnSync();
        internal void InvokeSyncFixed() => OnSyncFixed();
        internal void InvokeSyncLate() => OnSyncLate();

        /// <summary>Called once after the view is bound to <see cref="Entity"/>; override to initialize.</summary>
        protected virtual void OnBind() { }
        /// <summary>Called once before the view is unbound and despawned; override to tear down.</summary>
        protected virtual void OnUnbind() { }
        /// <summary>Called each Update phase to sync view state from the entity.</summary>
        protected virtual void OnSync() { }
        /// <summary>Called each FixedUpdate phase to sync view state from the entity.</summary>
        protected virtual void OnSyncFixed() { }
        /// <summary>Called each LateUpdate phase to sync view state from the entity.</summary>
        protected virtual void OnSyncLate() { }
    }
}
