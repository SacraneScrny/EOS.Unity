#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using EOS.CodeGen;

namespace EOS.Unity.Editor
{
    // Keeps the zero-alloc system registry in sync with the systems in the project.
    // After every script reload it re-emits SystemRegistryGenerator.BuildSource and
    // writes it only when the content changed, so the post-import recompile reaches a
    // fixed point instead of looping.
    //
    // The generated file's existence is the opt-in: it is created only on demand via
    // "Sackrany/EOS/Create System Registry". While it exists it is kept in sync on every
    // recompile; if it doesn't exist nothing is generated and SystemsRunner falls back to
    // reflection. The emitted file registers itself through a [ModuleInitializer].
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
            // Keep an existing registry in sync, but never create it automatically.
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

            // Only touch the file when something changed — this is what stops the
            // write -> import -> reload -> regenerate cycle from spinning forever.
            if (File.Exists(OutputPath) && File.ReadAllText(OutputPath) == source)
                return;

            Directory.CreateDirectory(OutputDir);
            File.WriteAllText(OutputPath, source);
            AssetDatabase.ImportAsset(OutputPath);
        }
    }
}
#endif
