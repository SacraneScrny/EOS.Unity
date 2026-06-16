using System;
using EOS.Core;
using EOS.Entities;
using EOS.Extensions;
using EOS.Logging;
using EOS.Objects;
using UnityEngine;

namespace EOS.Unity
{
    /// <summary>Typed, reflection-free entity assembler handed to <see cref="EntityBlueprint.Configure"/>: adds components (pooled when they implement <c>IPoolableObject</c>), incarnations and tags by direct generic calls, and composes nested entities by hierarchy or assembly socket. The entity is created inactive; the blueprint activates it after configuration.</summary>
    public readonly struct EntityBuilder
    {
        readonly EosEntity _entity;
        readonly World _world;

        internal EntityBuilder(EosEntity entity, World world)
        {
            _entity = entity;
            _world = world;
        }

        /// <summary>The entity being assembled (still inactive during configuration); use it for operations the builder does not wrap.</summary>
        public EosEntity Entity => _entity;

        /// <summary>The world the entity belongs to; nested blueprints are built into this same world.</summary>
        public World World => _world;

        /// <summary>Adds a component of type <typeparamref name="T"/> (rented from the pool when <typeparamref name="T"/> is poolable) and returns it for direct field assignment; the zero-allocation form for hot spawns.</summary>
        public T Add<T>() where T : EosObject, new() => _entity.Add<T>();

        /// <summary>Adds a component of type <typeparamref name="T"/> (pooled when poolable), runs <paramref name="configure"/> on it, and returns the builder for chaining; the callback allocates a closure if it captures, so prefer the returning <see cref="Add{T}()"/> on hot paths.</summary>
        public EntityBuilder Add<T>(Action<T> configure) where T : EosObject, new()
        {
            var component = _entity.Add<T>();
            configure?.Invoke(component);
            return this;
        }

        /// <summary>Attaches an incarnation (view) of type <typeparamref name="TView"/> resolved by <paramref name="id"/> through the registered binder (the view is pooled when its prefab opts in); returns the builder.</summary>
        public EntityBuilder AddIncarnation<TView>(string id) where TView : class
        {
            _entity.Add<Incarnation<TView>>().Setup(id);
            return this;
        }

        /// <summary>Attaches an incarnation by <paramref name="kind"/> (<see cref="EntityIncarnation"/> or <see cref="GameObject"/>); <see cref="IncarnationViewKind.None"/> is a no-op. Returns the builder.</summary>
        public EntityBuilder AddIncarnation(IncarnationViewKind kind, string id)
        {
            switch (kind)
            {
                case IncarnationViewKind.EntityIncarnation: return AddIncarnation<EntityIncarnation>(id);
                case IncarnationViewKind.GameObject: return AddIncarnation<GameObject>(id);
                default: return this;
            }
        }

        /// <summary>Adds a tag (string or enum value) to the entity; returns the builder.</summary>
        public EntityBuilder Tag(object tag)
        {
            _entity.AddTag(tag);
            return this;
        }

        /// <summary>Parents this entity under <paramref name="parent"/> in the native hierarchy; returns the builder.</summary>
        public EntityBuilder Parent(EosEntity parent)
        {
            _entity.SetParent(parent);
            return this;
        }

        /// <summary>Creates a bare child entity parented under this entity in the native hierarchy and returns it; its effective active state follows this (still-inactive) parent until the blueprint activates it.</summary>
        public EosEntity CreateChild(string name = "", bool active = true, bool serializable = true)
            => _entity.CreateChild(name, active, serializable);

        /// <summary>Builds <paramref name="blueprint"/> into the same world and parents the result under this entity (native hierarchy); returns the child, or <see cref="EosEntity.Null"/> on a null blueprint or build failure.</summary>
        public EosEntity AddChild(EntityBlueprint blueprint)
        {
            if (blueprint == null) return EosEntity.Null;
            var child = blueprint.Build(_world);
            if (child.IsValid) child.SetParent(_entity);
            return child;
        }

        /// <summary>Parents an already-built entity under this entity in the native hierarchy; returns the builder.</summary>
        public EntityBuilder AddChild(EosEntity child)
        {
            if (child.IsValid) child.SetParent(_entity);
            return this;
        }

        /// <summary>Builds <paramref name="blueprint"/> into the same world and attaches it into <paramref name="socketId"/> on this entity (typed assembly link plus native parent); destroys the orphan and returns <see cref="EosEntity.Null"/> if the attach fails.</summary>
        public EosEntity AttachModule(string socketId, EntityBlueprint blueprint)
        {
            if (blueprint == null) return EosEntity.Null;
            return AttachBuilt(socketId, blueprint.Build(_world), false, Vector3.zero, Quaternion.identity);
        }

        /// <summary>Builds <paramref name="blueprint"/> and attaches it into <paramref name="socketId"/> on this entity with the given local position/rotation offset; destroys the orphan and returns <see cref="EosEntity.Null"/> on attach failure.</summary>
        public EosEntity AttachModule(string socketId, EntityBlueprint blueprint, Vector3 localPosition, Quaternion localRotation)
        {
            if (blueprint == null) return EosEntity.Null;
            return AttachBuilt(socketId, blueprint.Build(_world), true, localPosition, localRotation);
        }

        /// <summary>Attaches an already-built entity into <paramref name="socketId"/> on this entity; returns the builder (the entity is left to the caller on failure, not destroyed).</summary>
        public EntityBuilder AttachModule(string socketId, EosEntity module)
        {
            if (module.IsValid) module.AttachTo(_entity, socketId);
            return this;
        }

        EosEntity AttachBuilt(string socketId, EosEntity module, bool applyOffset, Vector3 localPosition, Quaternion localRotation)
        {
            if (!module.IsValid) return EosEntity.Null;

            bool attached = applyOffset
                ? module.AttachTo(_entity, socketId, localPosition, localRotation)
                : module.AttachTo(_entity, socketId);

            if (!attached)
            {
                EosLog.Warning($"module could not attach to socket '{socketId}'", nameof(EntityBuilder));
                module.Destroy();
                return EosEntity.Null;
            }

            if (_world != null && _world.IsIterating)
                _world.AfterCurrentPhase.Schedule(module).If(e => !e.Has<AttachedTo>()).Destroy();

            return module;
        }
    }
}
