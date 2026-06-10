using System;
using EOS.Entities;
using EOS.Loader;
using EOS.Logging;
using UnityEngine;

namespace EOS.Unity
{
    public sealed class GameObjectBinder : IIncarnationBinder<GameObject>
    {
        public GameObject Instantiate(EosEntity entity, string incarnationId)
        {
            try
            {
                var prefab = IncarnationDatabase.Resolve(incarnationId);
                if (prefab == null) return null;
                return ViewPoolRegistry.Spawn(prefab);
            }
            catch (Exception ex)
            {
                EosLog.Error($"instantiate '{incarnationId}' threw: {ex}", nameof(GameObjectBinder));
                return null;
            }
        }

        public void Destroy(EosEntity entity, GameObject view)
        {
            if (view != null) ViewPoolRegistry.Despawn(view);
        }

        public void Sync(EosEntity entity, GameObject view) { }
        public void SyncFixed(EosEntity entity, GameObject view) { }
        public void SyncLate(EosEntity entity, GameObject view) { }
    }
}
