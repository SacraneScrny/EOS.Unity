using System;
using EOS.Entities;
using EOS.Loader;
using EOS.Logging;
using UnityEngine;

namespace EOS.Unity
{
    /// <summary>
    /// Incarnation binder for <see cref="EntityIncarnation"/> prefabs: spawns (pooled) views, binds them to the entity, and
    /// dispatches per-phase sync to the view's <c>On*</c> hooks. Registered as a default binder during boot.
    /// </summary>
    public sealed class EntityIncarnationBinder : IIncarnationBinder<EntityIncarnation>
    {
        /// <summary>Spawns the prefab for <paramref name="incarnationId"/>, binds the <see cref="EntityIncarnation"/> to the entity, and returns it (null on failure).</summary>
        public EntityIncarnation Instantiate(EosEntity entity, string incarnationId)
        {
            if (EntityViewAdoption.TryAdopt(entity, out var adopted))
            {
                try
                {
                    adopted.Entity = entity;
                    adopted.InvokeBind();
                    return adopted;
                }
                catch (Exception ex)
                {
                    EosLog.Error($"adopt '{incarnationId}' threw: {ex}", nameof(EntityIncarnationBinder));
                    if (adopted != null) ViewPoolRegistry.Despawn(adopted.gameObject);
                    return null;
                }
            }

            GameObject instance = null;
            try
            {
                var prefab = IncarnationDatabase.Resolve(incarnationId);
                if (prefab == null) return null;

                instance = ViewPoolRegistry.Spawn(prefab);
                var view = instance.GetComponent<EntityIncarnation>();
                if (view == null)
                {
                    EosLog.Error($"prefab '{incarnationId}' has no EntityIncarnation component", nameof(EntityIncarnationBinder));
                    ViewPoolRegistry.Despawn(instance);
                    return null;
                }

                view.Entity = entity;
                view.InvokeBind();
                return view;
            }
            catch (Exception ex)
            {
                EosLog.Error($"instantiate '{incarnationId}' threw: {ex}", nameof(EntityIncarnationBinder));
                if (instance != null) ViewPoolRegistry.Despawn(instance);
                return null;
            }
        }

        /// <summary>Unbinds the view, clears its entity, and despawns it back to the pool.</summary>
        public void Destroy(EosEntity entity, EntityIncarnation view)
        {
            if (view == null) return;
            try { view.InvokeUnbind(); }
            catch (Exception ex) { EosLog.Error($"OnUnbind threw: {ex.Message}", nameof(EntityIncarnationBinder)); }
            view.Entity = EosEntity.Null;
            if (view != null) ViewPoolRegistry.Despawn(view.gameObject);
        }

        /// <summary>Dispatches the Update-phase sync to the view's <c>OnSync</c>.</summary>
        public void Sync(EosEntity entity, EntityIncarnation view)
        {
            if (view == null) return;
            try { view.InvokeSync(); }
            catch (Exception ex) { EosLog.Error($"OnSync threw: {ex.Message}", nameof(EntityIncarnationBinder)); }
        }

        /// <summary>Dispatches the FixedUpdate-phase sync to the view's <c>OnSyncFixed</c>.</summary>
        public void SyncFixed(EosEntity entity, EntityIncarnation view)
        {
            if (view == null) return;
            try { view.InvokeSyncFixed(); }
            catch (Exception ex) { EosLog.Error($"OnSyncFixed threw: {ex.Message}", nameof(EntityIncarnationBinder)); }
        }

        /// <summary>Dispatches the LateUpdate-phase sync to the view's <c>OnSyncLate</c>.</summary>
        public void SyncLate(EosEntity entity, EntityIncarnation view)
        {
            if (view == null) return;
            try { view.InvokeSyncLate(); }
            catch (Exception ex) { EosLog.Error($"OnSyncLate threw: {ex.Message}", nameof(EntityIncarnationBinder)); }
        }
    }
}
