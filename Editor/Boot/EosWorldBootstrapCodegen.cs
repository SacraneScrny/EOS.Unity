#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using EOS.Core;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace EOS.Unity.Editor
{
    // Self-assembling per-world bootstrap generator. After every script reload it collects all
    // [EosWorldBootstrap] methods, sorts them (Order, then a stable type/method name tie-break) and
    // bakes the call list into a generated file. The generated Register(World) is invoked by the core
    // through EOS.Loader.WorldBootstrap.Provider on every World.Init() and World.Reset(), so the
    // default world and any world created later all run through the same steps. The file is only
    // rewritten when its content actually changes, so the post-import recompile reaches a fixed point
    // instead of looping.
    //
    // The generated file's existence is the opt-in: it is created only on demand via
    // "Sackrany/EOS/Create World Bootstrap". While it exists it is kept in sync on every recompile; if
    // it doesn't exist nothing is generated and the core provider stays null (Apply is a no-op). The
    // generated file installs itself into the core provider via RuntimeInitializeOnLoadMethod at
    // SubsystemRegistration — strictly before any boot path creates the default world.
    static class EosWorldBootstrapCodegen
    {
        const string OutputDir = "Assets/EOS.Generated";
        const string OutputPath = "Assets/EOS.Generated/WorldBootstrap.gen.cs";
        const string Tag = "[EOS]";

        [DidReloadScripts]
        static void OnScriptsReloaded()
        {
            // Keep an existing world bootstrap in sync, but never create it automatically.
            if (File.Exists(OutputPath))
                Generate();
        }

        [MenuItem("Sackrany/EOS/Create World Bootstrap")]
        static void CreateBootstrap()
        {
            bool existed = File.Exists(OutputPath);
            Generate();

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(OutputPath);
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }

            Debug.Log(existed ? $"{Tag} world bootstrap already exists — refreshed {OutputPath}"
                              : $"{Tag} created world bootstrap at {OutputPath}");
        }

        static void Generate()
        {
            var steps = Sort(Collect());
            var source = Emit(steps);

            // Only touch the file when something changed — this is what stops the
            // write -> import -> reload -> regenerate cycle from spinning forever.
            if (File.Exists(OutputPath) && File.ReadAllText(OutputPath) == source)
                return;

            Directory.CreateDirectory(OutputDir);
            File.WriteAllText(OutputPath, source);
            AssetDatabase.ImportAsset(OutputPath);
        }

        // ---- collection -------------------------------------------------------

        sealed class Node
        {
            public Type Type;
            public string MethodName;
            public int Order;

            public string Display => $"{Type.FullName}.{MethodName}";
            public string Member => "global::" + Type.FullName.Replace('+', '.') + "." + MethodName;
        }

        static List<Node> Collect()
        {
            var nodes = new List<Node>();

            foreach (var method in TypeCache.GetMethodsWithAttribute<EosWorldBootstrapAttribute>())
            {
                if (!Valid(method)) continue;

                var attr = method.GetCustomAttribute<EosWorldBootstrapAttribute>();
                nodes.Add(new Node
                {
                    Type = method.DeclaringType,
                    MethodName = method.Name,
                    Order = attr.Order,
                });
            }

            return nodes;
        }

        static bool Valid(MethodInfo method)
        {
            var type = method.DeclaringType;
            if (type == null) return false;

            var parameters = method.GetParameters();
            if (!method.IsStatic || method.IsGenericMethodDefinition || type.IsGenericTypeDefinition
                || type.FullName == null || !method.IsPublic || !IsAccessibleType(type)
                || method.ReturnType != typeof(void)
                || parameters.Length != 1 || parameters[0].ParameterType != typeof(World))
            {
                Debug.LogWarning(
                    $"{Tag} [EosWorldBootstrap] '{type?.FullName}.{method.Name}' must be a public static " +
                    "'void (World)' method on a public non-generic type; skipped.");
                return false;
            }

            return true;
        }

        static bool IsAccessibleType(Type type)
        {
            while (type.IsNested)
            {
                if (!type.IsNestedPublic) return false;
                type = type.DeclaringType;
            }
            return type.IsPublic;
        }

        // ---- ordering ---------------------------------------------------------

        static List<Node> Sort(List<Node> nodes)
        {
            nodes.Sort(Compare);
            return nodes;
        }

        static int Compare(Node a, Node b)
        {
            int c = a.Order.CompareTo(b.Order);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.Type.FullName, b.Type.FullName);
            return c != 0 ? c : string.CompareOrdinal(a.MethodName, b.MethodName);
        }

        // ---- emit -------------------------------------------------------------

        static string Emit(List<Node> steps)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated>");
            sb.AppendLine("//  Generated by EOS world-bootstrap codegen — do not edit by hand.");
            sb.AppendLine("//  Rebuilt automatically on each recompile from [EosWorldBootstrap] methods.");
            sb.AppendLine("// </auto-generated>");
            sb.AppendLine("namespace EOS.Unity.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    public static class WorldBootstrap");
            sb.AppendLine("    {");
            sb.AppendLine("        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]");
            sb.AppendLine("        static void Install()");
            sb.AppendLine("        {");
            sb.AppendLine("            global::EOS.Loader.WorldBootstrap.Provider = Register;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public static void Register(global::EOS.Core.World world)");
            sb.AppendLine("        {");
            foreach (var n in steps)
                sb.AppendLine($"            Invoke(() => {n.Member}(world), \"{n.Display}\");");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        static void Invoke(System.Action step, string name)");
            sb.AppendLine("        {");
            sb.AppendLine("            try { step(); }");
            sb.AppendLine("            catch (System.Exception e) { UnityEngine.Debug.LogError(\"[EOS] world bootstrap '\" + name + \"' threw: \" + e); }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}
#endif
