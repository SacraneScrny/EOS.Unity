using System.Collections.Generic;
using EOS.Objects;
using UnityEngine;

namespace EOS.Unity
{
    [CreateAssetMenu(menuName = "Sackrany/EOS/Component Set", fileName = "ComponentSet")]
    public sealed class EntityComponentSet : ScriptableObject
    {
        [Header("Tags")]
        [SerializeField] List<string> _tags = new();

        [Header("Components")]
        [SerializeReference]
        [SubclassSelector]
        [SerializeField]
        List<EosObject> _components = new();

        public IReadOnlyList<string> Tags => _tags;
        public IReadOnlyList<EosObject> Components => _components;
    }
}
