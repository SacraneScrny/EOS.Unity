using EOS.Entities;
using EOS.Logging;
using UnityEngine;

namespace EOS.Unity
{
    /// <summary>Scene component that spawns an <see cref="EntityActor"/> prefab at its own pose via <see cref="EntityActorSpawner"/>; requires EOS to be already booted (it does not boot).</summary>
    [AddComponentMenu("Sackrany/EOS/Entity Actor Spawn Point")]
    public class EntityActorSpawnPoint : MonoBehaviour
    {
        [SerializeField] GameObject _prefab;
        [SerializeField] bool _spawnOnStart = true;
        [SerializeField] bool _serializable;
        [SerializeField] bool _destroyAfterSpawn;

        /// <summary>The prefab this spawn point instantiates; assignable at runtime before calling <see cref="Spawn"/>.</summary>
        public GameObject Prefab
        {
            get => _prefab;
            set => _prefab = value;
        }

        /// <summary>The entity produced by the most recent <see cref="Spawn"/>, or <see cref="EosEntity.Null"/> if none/failed.</summary>
        public EosEntity LastSpawned { get; private set; } = EosEntity.Null;

        void Start()
        {
            if (_spawnOnStart) Spawn();
        }

        /// <summary>Spawns the prefab at this transform's pose into the default world and returns the entity (or <see cref="EosEntity.Null"/> on missing prefab / not booted); destroys this GameObject afterward if configured.</summary>
        public EosEntity Spawn()
        {
            if (_prefab == null)
            {
                EosLog.Warning($"'{name}' has no prefab assigned", nameof(EntityActorSpawnPoint));
                return EosEntity.Null;
            }

            if (!EosLoop.IsBooted)
            {
                EosLog.Error($"EOS is not booted; cannot spawn actor '{_prefab.name}'", nameof(EntityActorSpawnPoint));
                return EosEntity.Null;
            }

            LastSpawned = EntityActorSpawner.Spawn(_prefab, transform.position, transform.rotation, serializable: _serializable);

            if (_destroyAfterSpawn) Destroy(gameObject);

            return LastSpawned;
        }
    }
}
