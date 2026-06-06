using System;
using System.Collections.Generic;
using EOS.Logging;
using EOS.Objects;
using UnityEngine;

namespace EOS.Unity
{
    [CreateAssetMenu(menuName = "Sackrany/EOS/Component Set", fileName = "ComponentSet")]
    public sealed class EntityComponentSet : ScriptableObject
    {
        [Header("Code Module")]
        [SerializeReference]
        [SerializeField]
        ComponentSetModule _module;

        [Header("Tags")]
        [SerializeField] List<string> _tags = new();

        [Header("Components")]
        [SerializeReference]
        [SubclassSelector]
        [SerializeField]
        List<EosObject> _components = new();

        public IReadOnlyList<string> Tags => _tags;
        public IReadOnlyList<EosObject> Components => _components;
        public ComponentSetModule Module => _module;

        public ComponentSetBuilder Collect()
        {
            var builder = new ComponentSetBuilder();

            if (_components != null)
                for (int i = 0; i < _components.Count; i++)
                    builder.Add(_components[i]);

            if (_tags != null)
                for (int i = 0; i < _tags.Count; i++)
                    if (!string.IsNullOrEmpty(_tags[i]))
                        builder.AddTag(_tags[i]);

            if (_module != null)
            {
                try { _module.Build(builder); }
                catch (Exception ex) { EosLog.Error($"set '{name}' module build threw: {ex.Message}", nameof(EntityComponentSet)); }
            }

            return builder;
        }
    }
}
