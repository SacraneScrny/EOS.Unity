using UnityEngine;

namespace EOS.Unity
{
    /// <summary>Auto-added marker on a pooled instance that holds a reference to its owning pool so it can be returned.</summary>
    [AddComponentMenu("")]
    public sealed class PooledView : MonoBehaviour
    {
        internal GameObjectPool Owner;
    }
}
