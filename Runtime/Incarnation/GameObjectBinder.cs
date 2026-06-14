using System;
using EOS.Entities;
using EOS.Loader;
using EOS.Logging;
using UnityEngine;

namespace EOS.Unity
{
    /// <summary>
    /// Spawn-and-forget incarnation binder for plain <see cref="GameObject"/> prefabs; spawns (pooled) on instantiate, despawns
    /// on destroy, and performs no per-phase sync. Registered as a default binder during boot.
    /// </summary>
    public sealed class GameObjectBinder : IIncarnationBinder<GameObject>
    {
        /// <summary>Spawns the prefab for <paramref name="incarnationId"/> and returns the instance (null on failure).</summary>
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

        /// <summary>Despawns the view back to the pool.</summary>
        public void Destroy(EosEntity entity, GameObject view)
        {
            if (view != null) ViewPoolRegistry.Despawn(view);
        }

        /// <summary>No-op; this binder does not sync.</summary>
        public void Sync(EosEntity entity, GameObject view) { }
        /// <summary>No-op; this binder does not sync.</summary>
        public void SyncFixed(EosEntity entity, GameObject view) { }
        /// <summary>No-op; this binder does not sync.</summary>
        public void SyncLate(EosEntity entity, GameObject view) { }
    }
}
