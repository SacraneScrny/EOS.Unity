using System;
using System.Collections.Generic;

namespace EOS.Unity
{
    /// <summary>Serializable map of incarnation ids to prefab paths plus rename redirects, persisted as <c>incarnations.json</c>.</summary>
    [Serializable]
    public sealed class IncarnationIndex
    {
        /// <summary>The id-to-path entries.</summary>
        public List<IncarnationEntry> Entries = new();
        /// <summary>Old-id to new-id redirects that keep old saves resolvable across prefab renames.</summary>
        public List<IncarnationRedirect> Redirects = new();
    }

    /// <summary>One incarnation index entry mapping an id to its prefab Resources path.</summary>
    [Serializable]
    public sealed class IncarnationEntry
    {
        /// <summary>The incarnation id (the prefab path under <c>Resources/Incarnations/</c> without extension).</summary>
        public string Id;
        /// <summary>The prefab path passed to <see cref="UnityEngine.Resources.Load"/>.</summary>
        public string Path;
    }

    /// <summary>A rename redirect pointing an old incarnation id at its new id.</summary>
    [Serializable]
    public sealed class IncarnationRedirect
    {
        /// <summary>The retired incarnation id.</summary>
        public string OldId;
        /// <summary>The id the old id now resolves to.</summary>
        public string NewId;
    }
}
