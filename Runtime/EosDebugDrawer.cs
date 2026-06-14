using EOS.Core;
using UnityEngine;

namespace EOS.Unity
{
    /// <summary>Hidden play-mode-only MonoBehaviour whose <c>OnDrawGizmos</c> fans out to <c>Universe.DebugDraw()</c>.</summary>
    [AddComponentMenu("")]
    public sealed class EosDebugDrawer : MonoBehaviour
    {
        static EosDebugDrawer _instance;

        /// <summary>Creates the hidden drawer GameObject if one does not already exist; idempotent.</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("EOS Debug Drawer") { hideFlags = HideFlags.DontSave };
            _instance = go.AddComponent<EosDebugDrawer>();
        }

        /// <summary>Destroys the drawer GameObject if present; idempotent.</summary>
        public static void Remove()
        {
            if (_instance == null) return;
            if (Application.isPlaying) Destroy(_instance.gameObject);
            else DestroyImmediate(_instance.gameObject);
            _instance = null;
        }

        void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            Universe.DebugDraw();
        }
    }
}
