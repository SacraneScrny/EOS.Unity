using UnityEngine;

namespace EOS.Unity
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Sackrany/EOS/Incarnation Pooling")]
    public sealed class IncarnationPooling : MonoBehaviour
    {
        [SerializeField, Min(0)] int _preload;
        [SerializeField, Min(1)] int _maxSize = 32;

        public int Preload => _preload;
        public int MaxSize => _maxSize;
    }
}
