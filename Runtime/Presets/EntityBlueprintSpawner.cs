using EOS.Entities;
using EOS.Logging;
using UnityEngine;

namespace EOS.Unity
{
    /// <summary>Scene component that builds a code-defined <see cref="EntityBlueprint"/> (picked via a <c>[SerializeReference, SubclassSelector]</c> field) into the default world; requires EOS to be already booted (it does not boot).</summary>
    [AddComponentMenu("Sackrany/EOS/Entity Blueprint Spawner")]
    public class EntityBlueprintSpawner : MonoBehaviour
    {
        [SerializeReference]
        [SubclassSelector]
        EntityBlueprint _blueprint;

        [SerializeField] bool _spawnOnStart = true;
        [SerializeField] bool _destroyAfterSpawn;

        /// <summary>The blueprint this spawner builds; assignable at runtime before calling <see cref="Spawn"/>.</summary>
        public EntityBlueprint Blueprint
        {
            get => _blueprint;
            set => _blueprint = value;
        }

        /// <summary>The entity produced by the most recent <see cref="Spawn"/>, or <see cref="EosEntity.Null"/> if none/failed.</summary>
        public EosEntity LastSpawned { get; private set; } = EosEntity.Null;

        void Start()
        {
            if (_spawnOnStart)
            {
                Spawn();
            }
        }

        /// <summary>Builds the assigned blueprint into the default world and returns the entity (or <see cref="EosEntity.Null"/> on missing blueprint / not booted); destroys this GameObject afterward if configured.</summary>
        public EosEntity Spawn()
        {
            if (_blueprint == null)
            {
                EosLog.Warning($"'{name}' has no blueprint assigned", nameof(EntityBlueprintSpawner));
                return EosEntity.Null;
            }

            if (!EosLoop.IsBooted)
            {
                EosLog.Error($"EOS is not booted; cannot build blueprint '{_blueprint.GetType().Name}'", nameof(EntityBlueprintSpawner));
                return EosEntity.Null;
            }

            LastSpawned = _blueprint.Build();

            if (_destroyAfterSpawn)
                Destroy(gameObject);

            return LastSpawned;
        }
    }
}
