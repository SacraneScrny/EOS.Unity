using System;
using EOS.Entities;
using EOS.Loader;
using EOS.Logging;
using UnityEngine;

namespace EOS.Unity
{
    public sealed class EntityIncarnationBinder : IIncarnationBinder<EntityIncarnation>
    {
        public EntityIncarnation Instantiate(EosEntity entity, string incarnationId)
        {
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

        public void Destroy(EosEntity entity, EntityIncarnation view)
        {
            if (view == null) return;
            try { view.InvokeUnbind(); }
            catch (Exception ex) { EosLog.Error($"OnUnbind threw: {ex.Message}", nameof(EntityIncarnationBinder)); }
            view.Entity = EosEntity.Null;
            if (view != null) ViewPoolRegistry.Despawn(view.gameObject);
        }

        public void Sync(EosEntity entity, EntityIncarnation view)
        {
            if (view == null) return;
            try { view.InvokeSync(); }
            catch (Exception ex) { EosLog.Error($"OnSync threw: {ex.Message}", nameof(EntityIncarnationBinder)); }
        }

        public void SyncFixed(EosEntity entity, EntityIncarnation view)
        {
            if (view == null) return;
            try { view.InvokeSyncFixed(); }
            catch (Exception ex) { EosLog.Error($"OnSyncFixed threw: {ex.Message}", nameof(EntityIncarnationBinder)); }
        }

        public void SyncLate(EosEntity entity, EntityIncarnation view)
        {
            if (view == null) return;
            try { view.InvokeSyncLate(); }
            catch (Exception ex) { EosLog.Error($"OnSyncLate threw: {ex.Message}", nameof(EntityIncarnationBinder)); }
        }
    }
}
