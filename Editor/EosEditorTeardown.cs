#if UNITY_EDITOR
using UnityEditor;

namespace EOS.Unity.Editor
{
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
