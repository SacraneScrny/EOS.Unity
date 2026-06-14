using System.Collections.Generic;
using UnityEngine;

namespace EOS.Unity
{
    /// <summary>Authoring asset listing the available module-kind names; surfaced as a dropdown by <see cref="ModuleKindFieldAttribute"/>.</summary>
    [CreateAssetMenu(menuName = "Sackrany/EOS/Module Kind Catalog", fileName = "ModuleKindCatalog")]
    public sealed class ModuleKindCatalog : ScriptableObject
    {
        [SerializeField] List<string> _kinds = new();

        /// <summary>The module-kind names defined by this catalog.</summary>
        public IReadOnlyList<string> Kinds => _kinds;
    }
}
