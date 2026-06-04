using System;
using System.Collections.Generic;
using EOS.Logging;
using UnityEngine;

namespace EOS.Unity
{
    public static class IncarnationDatabase
    {
        public const string ResourceKey = "incarnations";

        static Dictionary<string, string> _idToPath;
        static Dictionary<string, string> _redirects;

        public static bool IsLoaded => _idToPath != null;

        public static void Load()
        {
            _idToPath = new Dictionary<string, string>();
            _redirects = new Dictionary<string, string>();

            try
            {
                var text = Resources.Load<TextAsset>(ResourceKey);
                if (text == null)
                {
                    EosLog.Warning($"index '{ResourceKey}' not found in Resources", nameof(IncarnationDatabase));
                    return;
                }

                var index = JsonUtility.FromJson<IncarnationIndex>(text.text);
                if (index == null) return;

                foreach (var e in index.Entries)
                    if (!string.IsNullOrEmpty(e.Id)) _idToPath[e.Id] = e.Path;

                foreach (var r in index.Redirects)
                    if (!string.IsNullOrEmpty(r.OldId)) _redirects[r.OldId] = r.NewId;
            }
            catch (Exception ex)
            {
                EosLog.Error($"failed to load index: {ex.Message}", nameof(IncarnationDatabase));
            }
        }

        public static void Unload()
        {
            _idToPath = null;
            _redirects = null;
        }

        public static GameObject Resolve(string id)
        {
            if (_idToPath == null) Load();

            var resolved = ResolveId(id);
            if (resolved == null || !_idToPath.TryGetValue(resolved, out var path))
            {
                EosLog.Error($"id '{id}' not in index", nameof(IncarnationDatabase));
                return null;
            }

            var prefab = Resources.Load<GameObject>(path);
            if (prefab == null)
                EosLog.Error($"Resources.Load failed for '{path}' (id '{id}')", nameof(IncarnationDatabase));

            return prefab;
        }

        static string ResolveId(string id)
        {
            if (id == null) return null;

            var current = id;
            int guard = 0;
            while (_redirects.TryGetValue(current, out var next) && guard++ < 64)
                current = next;

            return current;
        }
    }
}
