using EOS.Entities;
using EOS.Logging;
using UnityEngine;

namespace EOS.Unity
{
    [AddComponentMenu("Sackrany/EOS/Entity Preset Spawner")]
    public class EntityPresetSpawner : MonoBehaviour
    {
        [SerializeField] EntityPreset _preset;
        [SerializeField] bool _spawnOnStart = true;
        [SerializeField] bool _destroyAfterSpawn;

        public EntityPreset Preset
        {
            get => _preset;
            set => _preset = value;
        }

        public EosEntity LastSpawned { get; private set; } = EosEntity.Null;

        void Start()
        {
            if (_spawnOnStart)
            {
                Spawn();
            }
        }

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
