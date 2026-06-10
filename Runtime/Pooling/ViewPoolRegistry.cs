using System.Collections.Generic;
using UnityEngine;

namespace EOS.Unity
{
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

        public static GameObject Spawn(GameObject prefab)
        {
            if (prefab == null) return null;
            if (!prefab.TryGetComponent<IncarnationPooling>(out var pooling))
                return Object.Instantiate(prefab);
            return GetOrCreate(prefab, pooling).Rent();
        }

        public static void Despawn(GameObject instance)
        {
            if (instance == null) return;
            if (TryReturn(instance)) return;
            if (Application.isPlaying) Object.Destroy(instance);
            else Object.DestroyImmediate(instance);
        }

        public static bool TryReturn(GameObject instance)
        {
            if (instance == null) return false;
            if (!instance.TryGetComponent<PooledView>(out var marker) || marker.Owner == null) return false;
            marker.Owner.Return(instance);
            return true;
        }

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

        public static void Clear(GameObject prefab)
        {
            if (prefab == null) return;
            if (!_pools.TryGetValue(prefab, out var pool)) return;
            pool.Clear();
            _pools.Remove(prefab);
        }

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
