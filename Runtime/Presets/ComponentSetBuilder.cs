using System.Collections.Generic;
using EOS.Objects;

namespace EOS.Unity
{
    public sealed class ComponentSetBuilder
    {
        readonly List<EosObject> _components = new();
        readonly List<object> _tags = new();

        public IReadOnlyList<EosObject> Components => _components;
        public IReadOnlyList<object> Tags => _tags;

        public T Add<T>() where T : EosObject, new()
        {
            var component = new T();
            _components.Add(component);
            return component;
        }

        public T Add<T>(T component) where T : EosObject
        {
            if (component != null) _components.Add(component);
            return component;
        }

        public void Add(EosObject component)
        {
            if (component != null) _components.Add(component);
        }

        public void AddTag(object tag)
        {
            if (tag != null) _tags.Add(tag);
        }
    }
}
