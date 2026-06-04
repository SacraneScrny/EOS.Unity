using EOS.Core;
using UnityEngine;

namespace EOS.Unity
{
    [AddComponentMenu("")]
    public sealed class EosDebugDrawer : MonoBehaviour
    {
        static EosDebugDrawer _instance;

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("EOS Debug Drawer") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<EosDebugDrawer>();
        }

        public static void Remove()
        {
            if (_instance == null) return;
            Destroy(_instance.gameObject);
            _instance = null;
        }

        void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            Universe.DebugDraw();
        }
    }
}
