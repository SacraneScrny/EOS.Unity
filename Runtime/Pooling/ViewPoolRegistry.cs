using System.Collections.Generic;
using UnityEngine;

namespace EOS.Unity
{
    /// <summary>Central registry of per-prefab view pools; the spawn/despawn seam all binders route through, transparently pooling prefabs that carry <see cref="IncarnationPooling"/>.</summary>
    public static class ViewPoolRegistry
    {
        static readonly Dictionary<GameObject, GameObjectPool> _pools = new();
        static Transform _root;

        static Transform Root
        {
            get
            {
                if (_root == null)
                {
                    var go = new GameObject("EOS View Pool") { hideFlags = HideFlags.HideAndDontSave };
                    go.SetActive(false);
                    _root = go.transform;
                }
                return _root;
            }
        }

        /// <summary>Spawns an instance of the prefab, renting from its pool if it has <see cref="IncarnationPooling"/>, otherwise plain-instantiating.</summary>
        public static GameObject Spawn(GameObject prefab)
        {
            if (prefab == null) return null;
            if (!prefab.TryGetComponent<IncarnationPooling>(out var pooling))
                return Object.Instantiate(prefab);
            return GetOrCreate(prefab, pooling).Rent();
        }

        /// <summary>Returns a pooled instance to its pool, or destroys it if it is not pooled or the pool overflowed.</summary>
        public static void Despawn(GameObject instance)
        {
            if (instance == null) return;
            if (TryReturn(instance)) return;
            if (Application.isPlaying) Object.Destroy(instance);
            else Object.DestroyImmediate(instance);
        }

        /// <summary>Attempts to return the instance to its owning pool via its <see cref="PooledView"/> marker; returns false if it is not pooled.</summary>
        public static bool TryReturn(GameObject instance)
        {
            if (instance == null) return false;
            if (!instance.TryGetComponent<PooledView>(out var marker) || marker.Owner == null) return false;
            marker.Owner.Return(instance);
            return true;
        }

        /// <summary>Gets the pool for the prefab, creating one with the given max size if it does not exist yet.</summary>
        public static GameObjectPool GetOrCreate(GameObject prefab, int maxSize)
        {
            if (prefab == null) return null;
            if (_pools.TryGetValue(prefab, out var pool)) return pool;
            pool = new GameObjectPool(prefab, Root, maxSize);
            _pools.Add(prefab, pool);
            return pool;
        }

        static GameObjectPool GetOrCreate(GameObject prefab, IncarnationPooling pooling)
        {
            if (_pools.TryGetValue(prefab, out var pool)) return pool;
            pool = new GameObjectPool(prefab, Root, pooling.MaxSize);
            _pools.Add(prefab, pool);
            if (pooling.Preload > 0) pool.Prewarm(pooling.Preload);
            return pool;
        }

        /// <summary>Clears and removes the pool for a single prefab.</summary>
        public static void Clear(GameObject prefab)
        {
            if (prefab == null) return;
            if (!_pools.TryGetValue(prefab, out var pool)) return;
            pool.Clear();
            _pools.Remove(prefab);
        }

        /// <summary>Clears every pool and destroys the hidden pool root; runs on domain reset and on <c>EosLoop.Shutdown</c>.</summary>
        public static void ClearAll()
        {
            foreach (var pool in _pools.Values) pool.Clear();
            _pools.Clear();

            if (_root != null)
            {
                if (Application.isPlaying) Object.Destroy(_root.gameObject);
                else Object.DestroyImmediate(_root.gameObject);
                _root = null;
            }
        }

        [EosDomainReset]
        static void OnDomainReset() => ClearAll();
    }
}
