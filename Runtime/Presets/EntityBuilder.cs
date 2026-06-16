using System;
using EOS.Entities;
using EOS.Extensions;
using EOS.Objects;
using UnityEngine;

namespace EOS.Unity
{
    /// <summary>Typed, reflection-free entity assembler handed to <see cref="EntityBlueprint.Configure"/>: adds components (pooled when they implement <c>IPoolableObject</c>), incarnations and tags by direct generic calls, no field cloning. The entity is created inactive; the blueprint activates it after configuration.</summary>
    public readonly struct EntityBuilder
    {
        readonly EosEntity _entity;

        internal EntityBuilder(EosEntity entity) => _entity = entity;

        /// <summary>The entity being assembled (still inactive during configuration); use it for parenting or any operation the builder does not wrap.</summary>
        public EosEntity Entity => _entity;

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

        /// <summary>Parents the entity under <paramref name="parent"/> in the native hierarchy; returns the builder.</summary>
        public EntityBuilder Parent(EosEntity parent)
        {
            _entity.SetParent(parent);
            return this;
        }
    }
}
