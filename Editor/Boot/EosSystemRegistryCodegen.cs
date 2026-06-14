#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using EOS.CodeGen;

namespace EOS.Unity.Editor
{
    /// <summary>Editor driver for the core's <see cref="EOS.CodeGen.SystemRegistryGenerator"/>: writes <c>EosGeneratedSystems.gen.cs</c> (zero-alloc system bodies), opt-in by file existence and refreshed on recompile. Created via <c>Sackrany ▸ EOS ▸ Create System Registry</c>.</summary>
    static class EosSystemRegistryCodegen
    {
        const string OutputDir = "Assets/EOS.Generated";
        const string OutputPath = "Assets/EOS.Generated/EosGeneratedSystems.gen.cs";
        const string Namespace = "EOS.Generated";
        const string ClassName = "EosGeneratedSystems";
        const string Tag = "[EOS]";

        [DidReloadScripts]
        static void OnScriptsReloaded()
        {
            if (File.Exists(OutputPath))
                Generate();
        }

        [MenuItem("Sackrany/EOS/Create System Registry")]
        static void CreateRegistry()
        {
            bool existed = File.Exists(OutputPath);
            Generate();

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(OutputPath);
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }

            Debug.Log(existed ? $"{Tag} system registry already exists — refreshed {OutputPath}"
                              : $"{Tag} created system registry at {OutputPath}");
        }

        static void Generate()
        {
            var source = SystemRegistryGenerator.BuildSource(Namespace, ClassName);

            if (File.Exists(OutputPath) && File.ReadAllText(OutputPath) == source)
                return;

            Directory.CreateDirectory(OutputDir);
            File.WriteAllText(OutputPath, source);
            AssetDatabase.ImportAsset(OutputPath);
        }
    }
}
#endif
