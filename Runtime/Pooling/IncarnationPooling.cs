using UnityEngine;

namespace EOS.Unity
{
    /// <summary>Marker on an incarnation prefab root that opts it into view pooling and sets the preload count and max pool size.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sackrany/EOS/Incarnation Pooling")]
    public sealed class IncarnationPooling : MonoBehaviour
    {
        [SerializeField, Min(0)] int _preload;
        [SerializeField, Min(1)] int _maxSize = 32;

        /// <summary>Number of instances to prewarm when the pool is first created.</summary>
        public int Preload => _preload;
        /// <summary>Maximum number of parked instances the pool retains; overflow returns are destroyed.</summary>
        public int MaxSize => _maxSize;
    }
}
