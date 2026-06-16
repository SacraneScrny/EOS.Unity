using System;
using System.Collections.Generic;
using EOS.Core;
using EOS.Entities;
using EOS.Extensions;
using EOS.Logging;

namespace EOS.Unity
{
    /// <summary>Code-defined, reflection-free counterpart to <see cref="EntityPreset"/>: subclass it per entity kind, expose public fields as configs, and assemble the entity in <see cref="Configure"/> with the typed <see cref="EntityBuilder"/>. Select a concrete subclass in the inspector through a <c>[SerializeReference, SubclassSelector]</c> field. Spawns honor component and view pooling but skip the preset's field cloning.</summary>
    [Serializable]
    public abstract class EntityBlueprint
    {
        [ThreadStatic] static int _buildDepth;
        const int MaxBuildDepth = 32;

        /// <summary>Entity name; blank normalizes to <c>"Entity"</c>.</summary>
        public string Name;
        /// <summary>Whether the entity is activated after configuration so its components awake/start; leave false to build it suspended.</summary>
        public bool Active = true;
        /// <summary>Whether the entity is captured by world serialization.</summary>
        public bool Serializable = true;

        /// <summary>Which incarnation (view) binder to attach automatically; <see cref="IncarnationViewKind.None"/> (or a blank <see cref="IncarnationId"/>) leaves the entity viewless.</summary>
        public IncarnationViewKind IncarnationView = IncarnationViewKind.EntityIncarnation;
        /// <summary>The incarnation id (prefab under <c>Resources/Incarnations/</c>) the view resolves from; picked via the inspector dropdown. Don't also add an incarnation in <see cref="Configure"/> or the entity gets two views.</summary>
        [IncarnationId] public string IncarnationId;

        /// <summary>Module holders: socket id + nested blueprint, attached after <see cref="Configure"/>. The inspector picks each socket from the selected incarnation's <see cref="SocketSet"/> and the module via a subclass picker.</summary>
        public List<BlueprintModule> Modules = new();

        /// <summary>Builds the entity into <see cref="Universe.DefaultWorld"/>; logs and returns <see cref="EosEntity.Null"/> if EOS is not booted.</summary>
        public EosEntity Build()
        {
            if (Universe.DefaultWorld is World world)
                return Build(world);

            EosLog.Error("default world is not available; boot EOS before building blueprints", nameof(EntityBlueprint));
            return EosEntity.Null;
        }

        /// <summary>Builds the entity into <paramref name="world"/>: creates it inactive, runs <see cref="Configure"/>, then activates it if <see cref="Active"/>; on failure logs, destroys the partial entity, and returns <see cref="EosEntity.Null"/>.</summary>
        public EosEntity Build(World world)
        {
            if (world == null)
            {
                EosLog.Error("world is null", nameof(EntityBlueprint));
                return EosEntity.Null;
            }

            if (_buildDepth >= MaxBuildDepth)
            {
                EosLog.Error("blueprint nested too deep; aborting (cycle?)", nameof(EntityBlueprint));
                return EosEntity.Null;
            }

            var entity = EosEntity.Null;
            _buildDepth++;
            try
            {
                entity = new EosEntity(world, Name ?? string.Empty, false, Serializable);
                var builder = new EntityBuilder(entity, world);
                if (IncarnationView != IncarnationViewKind.None && !string.IsNullOrEmpty(IncarnationId))
                    builder.AddIncarnation(IncarnationView, IncarnationId);
                Configure(builder);
                if (Active) entity.On();
                ApplyModules(builder);
                return entity;
            }
            catch (Exception ex)
            {
                EosLog.Error($"build '{GetType().Name}' threw: {ex.Message}", nameof(EntityBlueprint));
                if (entity.IsValid) entity.Destroy();
                return EosEntity.Null;
            }
            finally
            {
                _buildDepth--;
            }
        }

        void ApplyModules(EntityBuilder builder)
        {
            if (Modules == null) return;
            for (int i = 0; i < Modules.Count; i++)
            {
                var entry = Modules[i];
                if (entry == null || entry.Module == null || string.IsNullOrEmpty(entry.SocketId)) continue;
                builder.AttachModule(entry.SocketId, entry.Module);
            }
        }

        /// <summary>Override to assemble the entity: add and configure components, attach an incarnation, and add tags through <paramref name="builder"/> using typed, reflection-free calls reading this blueprint's config fields.</summary>
        protected abstract void Configure(EntityBuilder builder);
    }
}
