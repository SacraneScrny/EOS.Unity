using System;
using EOS.Core;
using EOS.Entities;
using EOS.Extensions;
using EOS.Logging;
using EOS.Objects;
using UnityEngine;

namespace EOS.Unity
{
    public readonly struct EntityPresetBuilder
    {
        readonly World _world;
        readonly EosEntity _entity;

        internal EntityPresetBuilder(World world, EosEntity entity)
        {
            _world = world;
            _entity = entity;
        }

        public World World => _world;
        public EosEntity Entity => _entity;

        public T Add<T>() where T : EosObject, new()
        {
            try { return _entity.Add<T>(); }
            catch (Exception ex)
            {
                EosLog.Error($"add component '{typeof(T).Name}' threw: {ex.Message}", nameof(EntityPresetBuilder));
                return null;
            }
        }

        public T Add<T>(T template) where T : EosObject
        {
            if (template == null) return null;
            return (T)Add((EosObject)template);
        }

        public EosObject Add(EosObject template)
        {
            if (template == null) return null;
            try { return EntityPreset.AddComponentFrom(_world, _entity, template); }
            catch (Exception ex)
            {
                EosLog.Error($"add component '{template.GetType().Name}' threw: {ex.Message}", nameof(EntityPresetBuilder));
                return null;
            }
        }

        public void AddTag(object tag)
        {
            if (tag == null) return;
            try { _world.Tags.Add(_entity, tag); }
            catch (Exception ex) { EosLog.Error($"add tag threw: {ex.Message}", nameof(EntityPresetBuilder)); }
        }

        public Incarnation<TView> SetIncarnation<TView>(string id) where TView : class
        {
            if (string.IsNullOrEmpty(id)) return null;
            try
            {
                var incarnation = _entity.Add<Incarnation<TView>>();
                incarnation.Setup(id);
                return incarnation;
            }
            catch (Exception ex)
            {
                EosLog.Error($"set incarnation '{id}' threw: {ex.Message}", nameof(EntityPresetBuilder));
                return null;
            }
        }
    }
}
