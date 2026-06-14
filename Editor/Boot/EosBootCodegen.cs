#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace EOS.Unity.Editor
{
    static class EosBootCodegen
    {
        const string OutputDir = "Assets/EOS.Generated";
        const string OutputPath = "Assets/EOS.Generated/EosBootstrap.gen.cs";
        const string Tag = "[EOS]";

        [DidReloadScripts]
        static void OnScriptsReloaded()
        {
            if (File.Exists(OutputPath))
                Generate();
        }

        [MenuItem("Sackrany/EOS/Create Auto Bootstrap")]
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

            Debug.Log(existed ? $"{Tag} bootstrap already exists — refreshed {OutputPath}"
                              : $"{Tag} created bootstrap at {OutputPath}");
        }

        static void Generate()
        {
            var sortedProviders = Sort(CollectConfigProviders());
            var sortedSteps = Sort(CollectBootSteps());
            var source = Emit(sortedProviders, sortedSteps);

            if (File.Exists(OutputPath) && File.ReadAllText(OutputPath) == source)
                return;

            Directory.CreateDirectory(OutputDir);
            File.WriteAllText(OutputPath, source);
            AssetDatabase.ImportAsset(OutputPath);
        }

        sealed class Node
        {
            public Type Type;
            public string MethodName;
            public int Order;
            public bool IsFallback;
            public readonly List<(Type type, string method)> After = new();
            public readonly List<(Type type, string method)> Before = new();

            public (Type, string) Key => (Type, MethodName);
            public string Display => $"{Type.FullName}.{MethodName}";
            public string Member => "global::" + Type.FullName.Replace('+', '.') + "." + MethodName;
        }

        static List<Node> CollectBootSteps()
        {
            var nodes = new List<Node>();

            foreach (var method in TypeCache.GetMethodsWithAttribute<EosBootAttribute>())
            {
                if (!ValidBootStep(method)) continue;

                var boot = method.GetCustomAttribute<EosBootAttribute>();
                nodes.Add(NewNode(method, boot.Order, boot.IsFallback));
            }

            return nodes;
        }

        static List<Node> CollectConfigProviders()
        {
            var nodes = new List<Node>();

            foreach (var method in TypeCache.GetMethodsWithAttribute<EosBootConfigProviderAttribute>())
            {
                if (!ValidConfigProvider(method)) continue;

                var provider = method.GetCustomAttribute<EosBootConfigProviderAttribute>();
                nodes.Add(NewNode(method, provider.Order, false));
            }

            return nodes;
        }

        static Node NewNode(MethodInfo method, int order, bool isFallback)
        {
            var node = new Node
            {
                Type = method.DeclaringType,
                MethodName = method.Name,
                Order = order,
                IsFallback = isFallback,
            };

            foreach (var a in method.GetCustomAttributes<EosBootAfterAttribute>())
                node.After.Add((a.Type, a.Method));
            foreach (var b in method.GetCustomAttributes<EosBootBeforeAttribute>())
                node.Before.Add((b.Type, b.Method));

            return node;
        }

        static bool ValidBootStep(MethodInfo method)
        {
            var type = method.DeclaringType;
            if (type == null) return false;

            if (!method.IsStatic || method.GetParameters().Length != 0
                || method.IsGenericMethodDefinition || type.IsGenericTypeDefinition
                || type.FullName == null || !method.IsPublic || !IsAccessibleType(type))
            {
                Debug.LogWarning(
                    $"{Tag} [EosBoot] '{type.FullName}.{method.Name}' must be a public static parameterless " +
                    "method on a public non-generic type; skipped.");
                return false;
            }

            return true;
        }

        static bool ValidConfigProvider(MethodInfo method)
        {
            var type = method.DeclaringType;
            if (type == null) return false;

            var parameters = method.GetParameters();
            if (!method.IsStatic || method.IsGenericMethodDefinition || type.IsGenericTypeDefinition
                || type.FullName == null || !method.IsPublic || !IsAccessibleType(type)
                || parameters.Length != 1 || parameters[0].ParameterType != typeof(EosBootConfig)
                || method.ReturnType != typeof(EosBootConfig))
            {
                Debug.LogWarning(
                    $"{Tag} [EosBootConfigProvider] '{type.FullName}.{method.Name}' must be a public static method " +
                    "'EosBootConfig (EosBootConfig)' on a public non-generic type; skipped.");
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

        static List<Node> Sort(List<Node> nodes)
        {
            var byType = new Dictionary<Type, List<Node>>();
            var byKey = new Dictionary<(Type, string), Node>();
            foreach (var n in nodes)
            {
                if (!byType.TryGetValue(n.Type, out var list)) byType[n.Type] = list = new List<Node>();
                list.Add(n);
                byKey[n.Key] = n;
            }

            var adj = nodes.ToDictionary(n => n, _ => new HashSet<Node>());
            var indeg = nodes.ToDictionary(n => n, _ => 0);

            void Edge(Node from, Node to)
            {
                if (from == to) return;
                if (adj[from].Add(to)) indeg[to]++;
            }

            IEnumerable<Node> Resolve((Type type, string method) r)
            {
                if (r.type == null) yield break;
                if (r.method != null)
                {
                    if (byKey.TryGetValue((r.type, r.method), out var single)) yield return single;
                    yield break;
                }
                if (byType.TryGetValue(r.type, out var list))
                    foreach (var n in list) yield return n;
            }

            foreach (var n in nodes)
            {
                foreach (var r in n.After)
                    foreach (var dep in Resolve(r)) Edge(dep, n);
                foreach (var r in n.Before)
                    foreach (var dep in Resolve(r)) Edge(n, dep);
            }

            var ready = nodes.Where(n => indeg[n] == 0).ToList();
            var result = new List<Node>(nodes.Count);

            while (ready.Count > 0)
            {
                ready.Sort(Compare);
                var n = ready[0];
                ready.RemoveAt(0);
                result.Add(n);

                foreach (var v in adj[n])
                    if (--indeg[v] == 0) ready.Add(v);
            }

            if (result.Count != nodes.Count)
            {
                var stuck = nodes.Where(n => !result.Contains(n)).OrderBy(n => n.Display);
                Debug.LogError(
                    $"{Tag} boot order has a cycle in [EosBootBefore/After]; affected steps appended in Order: " +
                    string.Join(", ", stuck.Select(n => n.Display)));
                result.AddRange(nodes.Where(n => !result.Contains(n)).OrderBy(n => n, Comparer<Node>.Create(Compare)));
            }

            return result;
        }

        static int Compare(Node a, Node b)
        {
            int c = a.Order.CompareTo(b.Order);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.Type.FullName, b.Type.FullName);
            return c != 0 ? c : string.CompareOrdinal(a.MethodName, b.MethodName);
        }

        const string ConfigType = "global::EOS.Unity.EosBootConfig";

        static string Emit(List<Node> providers, List<Node> steps)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated>");
            sb.AppendLine("//  Generated by EOS bootstrap codegen — do not edit by hand.");
            sb.AppendLine("//  Rebuilt automatically on each recompile from [EosBoot] /");
            sb.AppendLine("//  [EosBootConfigProvider] methods.");
            sb.AppendLine("// </auto-generated>");
            sb.AppendLine("namespace EOS.Unity.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    public static class EosBootstrap");
            sb.AppendLine("    {");
            sb.AppendLine("        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]");
            sb.AppendLine("        public static void Run()");
            sb.AppendLine("        {");
            sb.AppendLine("            if (global::EOS.Unity.EosLoop.IsBooted)");
            sb.AppendLine("            {");
            foreach (var n in steps.Where(n => n.IsFallback))
                sb.AppendLine($"                Invoke(() => {n.Member}(), \"{n.Display}\");");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine();

            if (providers.Count > 0)
            {
                sb.AppendLine($"            var config = new {ConfigType}();");
                foreach (var n in providers)
                    sb.AppendLine($"            config = Provide(c => {n.Member}(c), config, \"{n.Display}\");");
                sb.AppendLine("            Invoke(() => global::EOS.Unity.EosLoop.Boot(config), \"EosLoop.Boot\");");
            }
            else
            {
                sb.AppendLine("            Invoke(() => global::EOS.Unity.EosLoop.Boot(), \"EosLoop.Boot\");");
            }

            foreach (var n in steps)
                sb.AppendLine($"            Invoke(() => {n.Member}(), \"{n.Display}\");");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        static void Invoke(System.Action step, string name)");
            sb.AppendLine("        {");
            sb.AppendLine("            try { step(); }");
            sb.AppendLine("            catch (System.Exception e) { UnityEngine.Debug.LogError(\"[EOS] boot step '\" + name + \"' threw: \" + e); }");
            sb.AppendLine("        }");

            if (providers.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"        static {ConfigType} Provide(System.Func<{ConfigType}, {ConfigType}> step, {ConfigType} config, string name)");
                sb.AppendLine("        {");
                sb.AppendLine("            try { return step(config) ?? config; }");
                sb.AppendLine("            catch (System.Exception e) { UnityEngine.Debug.LogError(\"[EOS] config provider '\" + name + \"' threw: \" + e); return config; }");
                sb.AppendLine("        }");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}
#endif
