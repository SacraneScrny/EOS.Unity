using System;
using System.Collections.Generic;
using EOS.Core;
using EOS.Entities;
using EOS.Extensions;
using EOS.Logging;
using EOS.Objects;
using UnityEngine;

namespace EOS.Unity
{
    /// <summary>ScriptableObject authoring template for an entity: name/active/serializable flags, incarnation, tags, component templates, referenced component sets with per-type overrides, and default assembly modules. Call <see cref="Instantiate()"/> to spawn it.</summary>
    [CreateAssetMenu(menuName = "Sackrany/EOS/Entity Preset", fileName = "EntityPreset")]
    public class EntityPreset : ScriptableObject
    {
        [SerializeField] string _entityName;
        [SerializeField] bool _active = true;
        [SerializeField] bool _serializable = true;

        [Header("Incarnation")]
        [SerializeField] IncarnationViewKind _incarnationView = IncarnationViewKind.EntityIncarnation;
        [SerializeField] string _incarnationId;

        [Header("Tags")]
        [SerializeField] List<string> _tags = new();

        [Header("Components")]
        [SerializeReference]
        [SubclassSelector]
        [SerializeField]
        List<EosObject> _components = new();

        [Header("Component Sets")]
        [SerializeField] List<EntityComponentSet> _sets = new();

        [SerializeReference]
        [SerializeField]
        List<EosObject> _setOverrides = new();

        [Header("Default Modules")]
        [SerializeField] List<DefaultModule> _defaultModules = new();

        public string EntityName => _entityName;
        public bool Active => _active;
        public IncarnationViewKind IncarnationView => _incarnationView;
        public string IncarnationId => _incarnationId;
        public IReadOnlyList<string> Tags => _tags;
        public IReadOnlyList<EosObject> Components => _components;
        public IReadOnlyList<EntityComponentSet> Sets => _sets;
        public IReadOnlyList<EosObject> SetOverrides => _setOverrides;
        public IReadOnlyList<DefaultModule> DefaultModules => _defaultModules;

        public EosEntity Instantiate()
        {
            if (Universe.DefaultWorld is World world)
                return Instantiate(world);

            EosLog.Error("default world is not available; boot EOS before instantiating presets", nameof(EntityPreset));
            return EosEntity.Null;
        }

        public EosEntity Instantiate(World world)
        {
            if (world == null)
            {
                EosLog.Error("world is null", nameof(EntityPreset));
                return EosEntity.Null;
            }

            var entity = EosEntity.Null;
            try
            {
                entity = new EosEntity(world, _entityName ?? string.Empty, false, _serializable);

                ApplySets(world, entity);
                ApplyTags(world, entity);
                ApplyIncarnation(entity);
                ApplyComponents(world, entity);

                if (_active) entity.On();

                AssemblyDefaults.Apply(world, entity, _defaultModules);
                return entity;
            }
            catch (Exception ex)
            {
                EosLog.Error($"instantiate '{name}' threw: {ex.Message}", nameof(EntityPreset));
                if (entity.IsValid) entity.Destroy();
                return EosEntity.Null;
            }
        }

        void ApplyTags(World world, EosEntity entity)
        {
            if (_tags == null) return;
            for (int i = 0; i < _tags.Count; i++)
            {
                var tag = _tags[i];
                if (string.IsNullOrEmpty(tag)) continue;
                try { world.Tags.Add(entity, tag); }
                catch (Exception ex) { EosLog.Error($"add tag '{tag}' threw: {ex.Message}", nameof(EntityPreset)); }
            }
        }

        void ApplyIncarnation(EosEntity entity)
        {
            if (string.IsNullOrEmpty(_incarnationId)) return;

            switch (_incarnationView)
            {
                case IncarnationViewKind.EntityIncarnation:
                    AddIncarnation<EntityIncarnation>(entity, _incarnationId);
                    break;
                case IncarnationViewKind.GameObject:
                    AddIncarnation<GameObject>(entity, _incarnationId);
                    break;
            }
        }

        static void AddIncarnation<TView>(EosEntity entity, string id) where TView : class
        {
            try { entity.Add<Incarnation<TView>>().Setup(id); }
            catch (Exception ex) { EosLog.Error($"add incarnation '{id}' threw: {ex.Message}", nameof(EntityPreset)); }
        }

        void ApplySets(World world, EosEntity entity)
        {
            if (_sets == null) return;

            var applied = new HashSet<Type>();
            for (int s = 0; s < _sets.Count; s++)
            {
                var set = _sets[s];
                if (set == null) continue;

                var collection = set.Collect();

                var components = collection.Components;
                for (int i = 0; i < components.Count; i++)
                {
                    var template = components[i];
                    if (template == null) continue;

                    var type = template.GetType();
                    if (!applied.Add(type)) continue;

                    var effective = FindOverride(type) ?? template;
                    try { AddComponentFrom(world, entity, effective); }
                    catch (Exception ex)
                    {
                        EosLog.Error($"add set component '{type.Name}' threw: {ex.Message}", nameof(EntityPreset));
                    }
                }

                var tags = collection.Tags;
                for (int i = 0; i < tags.Count; i++)
                {
                    var tag = tags[i];
                    if (tag == null) continue;
                    try { world.Tags.Add(entity, tag); }
                    catch (Exception ex)
                    {
                        EosLog.Error($"add set tag threw: {ex.Message}", nameof(EntityPreset));
                    }
                }
            }
        }

        EosObject FindOverride(Type type)
        {
            if (_setOverrides == null) return null;
            for (int i = 0; i < _setOverrides.Count; i++)
                if (_setOverrides[i] != null && _setOverrides[i].GetType() == type)
                    return _setOverrides[i];
            return null;
        }

        void ApplyComponents(World world, EosEntity entity)
        {
            if (_components == null) return;

            for (int i = 0; i < _components.Count; i++)
            {
                var template = _components[i];
                if (template == null) continue;

                try { AddComponentFrom(world, entity, template); }
                catch (Exception ex)
                {
                    EosLog.Error($"add component '{template.GetType().Name}' threw: {ex.Message}", nameof(EntityPreset));
                }
            }
        }

        static void AddComponentFrom(World world, EosEntity entity, EosObject template)
        {
            var storage = world.ObjectsStorages.GetOrCreate(template.GetType());
            var component = storage.AddObject(entity);
            EosCloneUtility.CopyDeclaredFields(template, component);
        }
    }
}
