#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EOS.Unity.Editor
{
    public sealed class IncarnationDatabaseWindow : EditorWindow
    {
        const string IndexAssetPath = "Assets/Resources/incarnations.json";

        Vector2 _scroll;
        IncarnationIndex _index;

        [MenuItem("Sackrany/EOS/Incarnation Database")]
        static void Open() => GetWindow<IncarnationDatabaseWindow>("Incarnations");

        void OnEnable() => Reload();

        void Reload()
        {
            _index = File.Exists(IndexAssetPath)
                ? JsonUtility.FromJson<IncarnationIndex>(File.ReadAllText(IndexAssetPath))
                : new IncarnationIndex();
        }

        void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rebuild")) { IncarnationIndexBuilder.Rebuild(); Reload(); }
                if (GUILayout.Button("Reload")) Reload();
            }

            if (_index == null)
            {
                EditorGUILayout.HelpBox("No index found.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"Entries: {_index.Entries.Count}", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var e in _index.Entries)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.SelectableLabel(e.Id, GUILayout.Height(16f));
                    EditorGUILayout.SelectableLabel(e.Path, GUILayout.Height(16f));
                }
            }

            if (_index.Redirects != null && _index.Redirects.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"Redirects: {_index.Redirects.Count}", EditorStyles.boldLabel);
                foreach (var r in _index.Redirects)
                    EditorGUILayout.LabelField(r.OldId, "-> " + r.NewId);
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
#endif
