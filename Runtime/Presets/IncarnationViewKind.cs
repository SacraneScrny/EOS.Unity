namespace EOS.Unity
{
    /// <summary>Selects which incarnation (view) binder a preset attaches to its entity, if any.</summary>
    public enum IncarnationViewKind
    {
        /// <summary>No incarnation; the entity has no view.</summary>
        None = 0,
        /// <summary>Bind through an <see cref="EntityIncarnation"/> view via <c>Incarnation&lt;EntityIncarnation&gt;</c>.</summary>
        EntityIncarnation = 1,
        /// <summary>Bind a plain <see cref="UnityEngine.GameObject"/> view via <c>Incarnation&lt;GameObject&gt;</c>.</summary>
        GameObject = 2,
    }
}
