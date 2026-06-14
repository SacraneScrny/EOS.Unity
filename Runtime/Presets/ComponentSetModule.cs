using System;

namespace EOS.Unity
{
    /// <summary>Base for a code-defined component-set contribution. Subclass and implement <see cref="Build"/>; assign it as the optional module on an <see cref="EntityComponentSet"/> via the inspector's <c>[SerializeReference]</c> picker.</summary>
    [Serializable]
    public abstract class ComponentSetModule
    {
        /// <summary>Declares the components and tags this module adds by populating <paramref name="builder"/>; called from <see cref="EntityComponentSet.Collect"/> (exceptions are caught and logged).</summary>
        public abstract void Build(ComponentSetBuilder builder);
    }
}
