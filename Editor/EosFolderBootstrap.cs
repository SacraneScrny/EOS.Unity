#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace EOS.Unity.Editor
{
    [InitializeOnLoad]
    static class EosFolderBootstrap
    {
        const string Resources = "Assets/Resources";
        const string Incarnations = "Assets/Resources/Incarnations";
        const string EntityPresets = "Assets/Resources/EntityPresets";
        const string ModuleCatalog = "Assets/Resources/ModuleKindCatalog.asset";

        static EosFolderBootstrap()
        {
            EditorApplication.delayCall += EnsureLayout;
        }

        [MenuItem("Sackrany/EOS/Create EOS Resources")]
        static void EnsureLayout()
        {
            bool created = EnsureFolder(Resources);
            created |= EnsureFolder(Incarnations);
            created |= EnsureFolder(EntityPresets);
            created |= EnsureModuleCatalog();

            if (created) AssetDatabase.Refresh();
        }

        static bool EnsureModuleCatalog()
        {
            if (AssetDatabase.FindAssets("t:ModuleKindCatalog").Length > 0) return false;

            var catalog = ScriptableObject.CreateInstance<ModuleKindCatalog>();
            AssetDatabase.CreateAsset(catalog, ModuleCatalog);
            return true;
        }

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
