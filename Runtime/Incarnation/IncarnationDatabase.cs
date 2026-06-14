using System;
using System.Collections.Generic;
using EOS.Logging;
using UnityEngine;

namespace EOS.Unity
{
    /// <summary>
    /// Loads <c>Resources/incarnations.json</c> and resolves incarnation ids (following redirects) to cached prefabs for the binders.
    /// </summary>
    public static class IncarnationDatabase
    {
        /// <summary>The Resources key of the incarnation index asset.</summary>
        public const string ResourceKey = "incarnations";

        static Dictionary<string, string> _idToPath;
        static Dictionary<string, string> _redirects;
        static Dictionary<string, GameObject> _prefabCache;

        /// <summary>True once the index has been loaded from Resources.</summary>
        public static bool IsLoaded => _idToPath != null;

        /// <summary>Loads the index from Resources, building the id-to-path and redirect tables; safe to call repeatedly.</summary>
        public static void Load()
        {
            _idToPath = new Dictionary<string, string>();
            _redirects = new Dictionary<string, string>();
            _prefabCache = new Dictionary<string, GameObject>();

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

        /// <summary>Clears the loaded tables and prefab cache; called on shutdown.</summary>
        public static void Unload()
        {
            _idToPath = null;
            _redirects = null;
            _prefabCache = null;
        }

        /// <summary>Resolves an incarnation id (following redirects) to its cached prefab, or null if not found; loads the index lazily.</summary>
        public static GameObject Resolve(string id)
        {
            if (_idToPath == null) Load();

            var resolved = ResolveId(id);
            if (resolved == null || !_idToPath.TryGetValue(resolved, out var path))
            {
                EosLog.Error($"id '{id}' not in index", nameof(IncarnationDatabase));
                return null;
            }

            if (_prefabCache.TryGetValue(resolved, out var cached) && cached != null)
                return cached;

            var prefab = Resources.Load<GameObject>(path);
            if (prefab == null)
            {
                EosLog.Error($"Resources.Load failed for '{path}' (id '{id}')", nameof(IncarnationDatabase));
                return null;
            }

            _prefabCache[resolved] = prefab;
            return prefab;
        }

        static string ResolveId(string id)
        {
            if (id == null) return null;

            var current = id;
            int guard = 0;
            while (_redirects != null && _redirects.TryGetValue(current, out var next))
            {
                if (guard++ >= 64)
                {
                    EosLog.Error($"Cyclic redirect detected for id '{id}'", nameof(IncarnationDatabase));
                    return null;
                }
                current = next;
            }

            return current;
        }
    }
}
