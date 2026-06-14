#if UNITY_EDITOR
using UnityEditor;

namespace EOS.Unity.Editor
{
    /// <summary>Shuts EOS down when exiting Play Mode so Enter-Play-Mode-Options (no domain reload) start from a clean state.</summary>
    [InitializeOnLoad]
    static class EosEditorTeardown
    {
        static EosEditorTeardown()
        {
            EditorApplication.playModeStateChanged -= OnStateChanged;
            EditorApplication.playModeStateChanged += OnStateChanged;
        }

        static void OnStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode && EosLoop.IsBooted)
                EosLoop.Shutdown();
        }
    }
}
#endif
