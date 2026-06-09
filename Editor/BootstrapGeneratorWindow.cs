#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace EOS.Unity.Editor
{
    // Generates a ready-to-use EOS bootstrap MonoBehaviour so you never have to
    // remember the correct boot pattern again: low execution order (boots before
    // everything else), an IsBooted guard (safe across scene loads), and a
    // serialized EosBootConfig you can tune in the inspector.
    public sealed class BootstrapGeneratorWindow : EditorWindow
    {
        const string FolderPrefKey = "EOS.BootstrapGenerator.Folder";

        string _folder = "Assets";
        string _className = "GameBootstrap";
        string _namespace = "";

        [MenuItem("Sackrany/EOS/Create Default Bootstrap")]
        static void Open()
        {
            var window = GetWindow<BootstrapGeneratorWindow>(true, "EOS Bootstrap");
            window.minSize = new Vector2(440, 230);
            window._folder = EditorPrefs.GetString(FolderPrefKey, "Assets");
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Generates a MonoBehaviour that boots EOS the right way: low execution " +
                "order, an IsBooted guard, and a serialized Config you tune in the inspector. " +
                "Drop the generated component on one GameObject in your first scene.",
                MessageType.Info);

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                _folder = EditorGUILayout.TextField("Folder", _folder);
                if (GUILayout.Button("Browse", GUILayout.Width(70)))
                    Browse();
            }

            _className = EditorGUILayout.TextField("Class name", _className);
            _namespace = EditorGUILayout.TextField("Namespace (optional)", _namespace);

            var path = TargetPath();
            EditorGUILayout.LabelField("Will create", path, EditorStyles.miniLabel);

            var error = Validate(path);
            if (error != null)
                EditorGUILayout.HelpBox(error, MessageType.Warning);

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(error != null))
                if (GUILayout.Button("Create Bootstrap", GUILayout.Height(28)))
                    Create(path);
        }

        void Browse()
        {
            var abs = EditorUtility.OpenFolderPanel("Select bootstrap folder", _folder, "");
            if (string.IsNullOrEmpty(abs)) return;

            var rel = ToProjectRelative(abs);
            if (rel == null)
            {
                EditorUtility.DisplayDialog("EOS", "The folder must be inside this project's Assets folder.", "OK");
                return;
            }

            _folder = rel;
            EditorPrefs.SetString(FolderPrefKey, _folder);
            GUI.FocusControl(null);
        }

        string TargetPath()
        {
            var folder = string.IsNullOrEmpty(_folder) ? "Assets" : _folder.Replace('\\', '/').TrimEnd('/');
            var name = string.IsNullOrEmpty(_className) ? "GameBootstrap" : _className;
            return $"{folder}/{name}.cs";
        }

        string Validate(string path)
        {
            if (!IsValidIdentifier(_className))
                return "Class name must be a valid C# identifier.";
            if (!string.IsNullOrEmpty(_namespace) && !IsValidNamespace(_namespace))
                return "Namespace is not a valid C# namespace.";
            if (!_folder.Replace('\\', '/').Equals("Assets"))
                return "Folder must be inside Assets.";
            if (File.Exists(path))
                return $"{path} already exists and will not be overwritten — rename or pick another folder.";
            return null;
        }

        void Create(string path)
        {
            EditorPrefs.SetString(FolderPrefKey, _folder);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, Template());
            AssetDatabase.ImportAsset(path);
            AssetDatabase.Refresh();

            var asset = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }

            Debug.Log($"[EOS] created bootstrap '{path}'");
            Close();
        }

        string Template()
        {
            var hasNs = !string.IsNullOrEmpty(_namespace);
            var pad = hasNs ? "    " : "";
            var sb = new StringBuilder();

            sb.AppendLine("using EOS.Unity;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();

            if (hasNs)
            {
                sb.AppendLine($"namespace {_namespace}");
                sb.AppendLine("{");
            }

            sb.AppendLine($"{pad}/// <summary>");
            sb.AppendLine($"{pad}/// Boots the EOS world before anything else runs. Put this on a single");
            sb.AppendLine($"{pad}/// GameObject in your first scene and tune the boot options via Config.");
            sb.AppendLine($"{pad}/// </summary>");
            sb.AppendLine($"{pad}[DisallowMultipleComponent]");
            sb.AppendLine($"{pad}[DefaultExecutionOrder(-10000)]");
            sb.AppendLine($"{pad}[AddComponentMenu(\"Sackrany/EOS/{_className}\")]");
            sb.AppendLine($"{pad}public sealed class {_className} : MonoBehaviour");
            sb.AppendLine($"{pad}{{");
            sb.AppendLine($"{pad}    [Tooltip(\"Profiler, debug draw and min log level. Register custom view binders in code via Config.AddBinder(...).\")]");
            sb.AppendLine($"{pad}    public EosBootConfig Config = new();");
            sb.AppendLine();
            sb.AppendLine($"{pad}    void Awake()");
            sb.AppendLine($"{pad}    {{");
            sb.AppendLine($"{pad}        if (EosLoop.IsBooted) return;");
            sb.AppendLine();
            sb.AppendLine($"{pad}        // Register custom incarnation binders here, e.g.:");
            sb.AppendLine($"{pad}        // Config.AddBinder(new MyShipBinder());");
            sb.AppendLine();
            sb.AppendLine($"{pad}        EosLoop.Boot(Config);");
            sb.AppendLine($"{pad}    }}");
            sb.AppendLine($"{pad}}}");

            if (hasNs)
                sb.AppendLine("}");

            return sb.ToString();
        }

        static string ToProjectRelative(string absolute)
        {
            absolute = absolute.Replace('\\', '/');
            var dataPath = Application.dataPath.Replace('\\', '/');
            if (absolute == dataPath) return "Assets";
            if (absolute.StartsWith(dataPath + "/")) return "Assets" + absolute.Substring(dataPath.Length);
            return null;
        }

        static bool IsValidIdentifier(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            if (!char.IsLetter(s[0]) && s[0] != '_') return false;
            for (int i = 1; i < s.Length; i++)
                if (!char.IsLetterOrDigit(s[i]) && s[i] != '_') return false;
            return true;
        }

        static bool IsValidNamespace(string s)
        {
            foreach (var part in s.Split('.'))
                if (!IsValidIdentifier(part)) return false;
            return true;
        }
    }
}
#endif
