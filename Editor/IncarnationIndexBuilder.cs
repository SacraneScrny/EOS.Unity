#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EOS.Unity.Editor
{
    /// <summary>Keeps <c>incarnations.json</c> in sync with the prefabs under <c>Resources/Incarnations/</c>, rebuilding on asset import and warning with paste-ready redirect snippets on renames.</summary>
    public sealed class IncarnationIndexBuilder : AssetPostprocessor
    {
        const string ResourcesRoot = "Assets/Resources/";
        const string IncarnationsFolder = "Assets/Resources/Incarnations";
        const string IndexAssetPath = "Assets/Resources/incarnations.json";

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!Affected(imported) && !Affected(deleted) && !Affected(moved) && !Affected(movedFrom))
                return;

            WarnOnRenames(moved, movedFrom);
            Rebuild();
        }

        /// <summary>Rebuilds the index from current prefabs while preserving any manually authored redirects, then writes and imports the JSON asset.</summary>
        [MenuItem("Sackrany/EOS/Rebuild Incarnation Index")]
        public static void Rebuild()
        {
            var index = new IncarnationIndex();

            if (Directory.Exists(IncarnationsFolder))
            {
                var guids = AssetDatabase.FindAssets("t:GameObject", new[] { IncarnationsFolder });
                var seen = new HashSet<string>();

                foreach (var guid in guids)
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (!IsPrefab(assetPath)) continue;

                    var id = ToId(assetPath);
                    if (!seen.Add(id))
                    {
                        Debug.LogError($"[EOS] duplicate incarnation id '{id}' at {assetPath}");
                        continue;
                    }

                    index.Entries.Add(new IncarnationEntry { Id = id, Path = ToResourcePath(assetPath) });
                }
            }

            PreserveRedirects(index);
            Write(index);
        }

        static void WarnOnRenames(string[] moved, string[] movedFrom)
        {
            if (moved == null || movedFrom == null) return;

            for (int i = 0; i < moved.Length && i < movedFrom.Length; i++)
            {
                bool wasInside = Under(movedFrom[i], IncarnationsFolder) && IsPrefab(movedFrom[i]);
                bool isInside = Under(moved[i], IncarnationsFolder) && IsPrefab(moved[i]);

                if (wasInside && isInside)
                {
                    var oldId = ToId(movedFrom[i]);
                    var newId = ToId(moved[i]);
                    if (oldId == newId) continue;

                    Debug.LogWarning(
                        $"[EOS] incarnation id changed '{oldId}' -> '{newId}'. " +
                        "Saves/references using the old id will break. To keep them, add a redirect to " +
                        $"incarnations.json: {{ \"OldId\": \"{oldId}\", \"NewId\": \"{newId}\" }}");
                }
                else if (wasInside)
                {
                    var oldId = ToId(movedFrom[i]);
                    Debug.LogWarning(
                        $"[EOS] incarnation '{oldId}' moved out of {IncarnationsFolder} and was removed from the index. " +
                        "Saves/references using this id will break.");
                }
            }
        }

        static void PreserveRedirects(IncarnationIndex index)
        {
            if (!File.Exists(IndexAssetPath)) return;
            try
            {
                var existing = JsonUtility.FromJson<IncarnationIndex>(File.ReadAllText(IndexAssetPath));
                if (existing?.Redirects != null) index.Redirects = existing.Redirects;
            }
            catch { }
        }

        static void Write(IncarnationIndex index)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(IndexAssetPath));
            File.WriteAllText(IndexAssetPath, JsonUtility.ToJson(index, true));
            AssetDatabase.ImportAsset(IndexAssetPath);
        }

        static bool Affected(string[] paths)
        {
            if (paths == null) return false;
            foreach (var p in paths)
                if (Under(p, IncarnationsFolder)) return true;
            return false;
        }

        static bool Under(string path, string folder)
            => !string.IsNullOrEmpty(path) && path.Replace('\\', '/').StartsWith(folder + "/");

        static bool IsPrefab(string path)
            => !string.IsNullOrEmpty(path) && path.EndsWith(".prefab");

        static string ToId(string assetPath)
        {
            var p = assetPath.Replace('\\', '/').Substring(IncarnationsFolder.Length + 1);
            return StripExtension(p);
        }

        static string ToResourcePath(string assetPath)
        {
            var p = assetPath.Replace('\\', '/').Substring(ResourcesRoot.Length);
            return StripExtension(p);
        }

        static string StripExtension(string path)
        {
            var dot = path.LastIndexOf('.');
            return dot >= 0 ? path.Substring(0, dot) : path;
        }
    }
}
#endif
