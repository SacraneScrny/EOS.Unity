using System;
using EOS.Core;
using EOS.Entities;
using EOS.Extensions;
using EOS.Logging;
using EOS.Objects;
using UnityEngine;

namespace EOS.Unity
{
    /// <summary>
    /// Static, reflection-free spawn API for the lazy prefab-first path: rents a pooled prefab carrying an
    /// <see cref="EntityActor"/>, creates and configures its entity (ECS-authoritative position via
    /// <see cref="EntityTransform"/>), and binds the prefab as the entity's view through the standard incarnation flow.
    /// Call from outside the system loop — like presets it makes structural changes that <c>StructuralChangePolicy</c>
    /// would otherwise reject mid-iteration.
    /// </summary>
    public static class EntityActorSpawner
    {
        /// <summary>Rents <paramref name="prefab"/> from its pool at the given pose, creates its entity in <paramref name="world"/> (default world if null) with an <see cref="EntityTransform"/>, runs the actor's <c>OnBuild</c> then <paramref name="configure"/>, and returns the entity (<see cref="EosEntity.Null"/> on failure).</summary>
        public static EosEntity Spawn(GameObject prefab, Vector3 position, Quaternion rotation, World world = null, bool serializable = false, string incarnationId = null, Action<EosEntity> configure = null)
        {
            if (prefab == null)
            {
                EosLog.Error("prefab is null", nameof(EntityActorSpawner));
                return EosEntity.Null;
            }

            if (!EosLoop.IsBooted)
            {
                EosLog.Error($"EOS is not booted; cannot spawn actor '{prefab.name}'", nameof(EntityActorSpawner));
                return EosEntity.Null;
            }

            world ??= Universe.DefaultWorld as World;
            if (world == null)
            {
                EosLog.Error("default world is not available", nameof(EntityActorSpawner));
                return EosEntity.Null;
            }

            GameObject instance = null;
            var entity = EosEntity.Null;
            try
            {
                instance = ViewPoolRegistry.Spawn(prefab);
                if (instance == null) return EosEntity.Null;

                var view = instance.GetComponent<EntityActor>();
                if (view == null)
                {
                    EosLog.Error($"prefab '{prefab.name}' has no EntityActor component", nameof(EntityActorSpawner));
                    ViewPoolRegistry.Despawn(instance);
                    return EosEntity.Null;
                }

                instance.transform.SetPositionAndRotation(position, rotation);

                entity = new EosEntity(world, prefab.name, false, serializable);

                var transform = entity.Add<EntityTransform>();
                transform.LocalPosition = position;
                transform.LocalRotation = rotation;
                transform.LocalScale = instance.transform.localScale;

                view.Entity = entity;
                view.InvokeBuild(entity);
                configure?.Invoke(entity);

                EntityViewAdoption.Offer(entity, view);
                entity.Add<Incarnation<EntityIncarnation>>().Setup(incarnationId ?? string.Empty);
                entity.On();

                return entity;
            }
            catch (Exception ex)
            {
                EosLog.Error($"spawn '{prefab.name}' threw: {ex}", nameof(EntityActorSpawner));
                if (entity.IsValid) entity.Destroy();
                if (instance != null) ViewPoolRegistry.Despawn(instance);
                return EosEntity.Null;
            }
        }

        /// <summary>Resolves the prefab for <paramref name="incarnationId"/> from the incarnation index and spawns it; see the prefab overload.</summary>
        public static EosEntity Spawn(string incarnationId, Vector3 position, Quaternion rotation, World world = null, bool serializable = false, Action<EosEntity> configure = null)
        {
            var prefab = IncarnationDatabase.Resolve(incarnationId);
            if (prefab == null) return EosEntity.Null;
            return Spawn(prefab, position, rotation, world, serializable, incarnationId, configure);
        }

        /// <summary>Despawns the actor's entity, returning its view to the pool; convenience over <c>entity.Destroy()</c>.</summary>
        public static void Despawn(EosEntity entity)
        {
            if (entity.IsValid) entity.Destroy();
        }
    }
}
