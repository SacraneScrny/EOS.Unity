using System.Collections.Generic;
using EOS.Objects;

namespace EOS.Unity
{
    /// <summary>Accumulator passed to a <see cref="ComponentSetModule"/> to declare the components and tags a component set contributes from code.</summary>
    public sealed class ComponentSetBuilder
    {
        readonly List<EosObject> _components = new();
        readonly List<object> _tags = new();

        /// <summary>Component templates collected so far, in declaration order.</summary>
        public IReadOnlyList<EosObject> Components => _components;
        /// <summary>Tags collected so far (string or enum values).</summary>
        public IReadOnlyList<object> Tags => _tags;

        /// <summary>Creates a fresh component of type <typeparamref name="T"/>, adds it to the set, and returns it for configuration.</summary>
        public T Add<T>() where T : EosObject, new()
        {
            var component = new T();
            _components.Add(component);
            return component;
        }

        /// <summary>Adds an existing component template (ignored if null) and returns it.</summary>
        public T Add<T>(T component) where T : EosObject
        {
            if (component != null) _components.Add(component);
            return component;
        }

        /// <summary>Adds an existing component template (ignored if null).</summary>
        public void Add(EosObject component)
        {
            if (component != null) _components.Add(component);
        }

        /// <summary>Adds a tag (string or enum value; ignored if null) the set will apply to the entity.</summary>
        public void AddTag(object tag)
        {
            if (tag != null) _tags.Add(tag);
        }
    }
}
