using EOS.Entities;
using EOS.Logging;
using UnityEngine;

namespace EOS.Unity
{
    /// <summary>Scene component that instantiates an <see cref="EntityPreset"/> into the default world; requires EOS to be already booted (it does not boot).</summary>
    [AddComponentMenu("Sackrany/EOS/Entity Preset Spawner")]
    public class EntityPresetSpawner : MonoBehaviour
    {
        [SerializeField] EntityPreset _preset;
        [SerializeField] bool _spawnOnStart = true;
        [SerializeField] bool _destroyAfterSpawn;

        /// <summary>The preset this spawner instantiates; assignable at runtime before calling <see cref="Spawn"/>.</summary>
        public EntityPreset Preset
        {
            get => _preset;
            set => _preset = value;
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

        /// <summary>Instantiates the assigned preset into the default world and returns the entity (or <see cref="EosEntity.Null"/> on missing preset / not booted); destroys this GameObject afterward if configured.</summary>
        public EosEntity Spawn()
        {
            if (_preset == null)
            {
                EosLog.Warning($"'{name}' has no preset assigned", nameof(EntityPresetSpawner));
                return EosEntity.Null;
            }

            if (!EosLoop.IsBooted)
            {
                EosLog.Error($"EOS is not booted; cannot spawn preset '{_preset.name}'", nameof(EntityPresetSpawner));
                return EosEntity.Null;
            }

            LastSpawned = _preset.Instantiate();

            if (_destroyAfterSpawn)
                Destroy(gameObject);

            return LastSpawned;
        }
    }
}
