using System.Collections.Generic;
using UnityEngine;

namespace EOS.Unity
{
    [CreateAssetMenu(menuName = "Sackrany/EOS/Module Kind Catalog", fileName = "ModuleKindCatalog")]
    public sealed class ModuleKindCatalog : ScriptableObject
    {
        [SerializeField] List<string> _kinds = new();

        public IReadOnlyList<string> Kinds => _kinds;
    }
}
