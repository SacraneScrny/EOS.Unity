using System;
using System.Collections.Generic;
using EOS.Logging;
using EOS.Objects;
using UnityEngine;

namespace EOS.Unity
{
    /// <summary>Reusable bundle of tags, component templates and an optional code module, referenced by one or more <see cref="EntityPreset"/>s to share common entity data.</summary>
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

        /// <summary>Tags this set applies to every referencing entity.</summary>
        public IReadOnlyList<string> Tags => _tags;
        /// <summary>Component templates this set contributes (one per type per entity).</summary>
        public IReadOnlyList<EosObject> Components => _components;
        /// <summary>Optional code module that adds further components/tags at collect time; may be null.</summary>
        public ComponentSetModule Module => _module;

        /// <summary>Materializes the set's tags and component templates (list first, then module) into a fresh <see cref="ComponentSetBuilder"/>; module exceptions are caught and logged.</summary>
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
