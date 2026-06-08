#if UNITY_EDITOR
using UnityEditor;

namespace EOS.Unity.Editor
{
    // Ensures the folders EOS relies on exist, so a fresh project does not have
    // to create them by hand. Runs automatically when the editor loads / scripts
    // recompile, and only touches the filesystem when a folder is missing.
    [InitializeOnLoad]
    static class EosFolderBootstrap
    {
        const string Resources = "Assets/Resources";
        const string Incarnations = "Assets/Resources/Incarnations";
        const string EntityPresets = "Assets/Resources/EntityPresets";

        static EosFolderBootstrap()
        {
            // Defer: AssetDatabase is not guaranteed ready inside the static ctor.
            EditorApplication.delayCall += EnsureFolders;
        }

        [MenuItem("Sackrany/EOS/Create EOS Folders")]
        static void EnsureFolders()
        {
            bool created = EnsureFolder(Resources);
            created |= EnsureFolder(Incarnations);
            created |= EnsureFolder(EntityPresets);

            if (created) AssetDatabase.Refresh();
        }

        // Creates the folder (and any missing parents) if it does not exist.
        // Returns true when something was created.
        static bool EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return false;

            var parent = path.Substring(0, path.LastIndexOf('/'));
            var name = path.Substring(path.LastIndexOf('/') + 1);

            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, name);
            return true;
        }
    }
}
#endif
