using System;
using System.Collections.Generic;
using EOS.Logging;
using UnityEngine;

namespace EOS.Unity
{
    /// <summary>
    /// An object pool for one prefab: parks deactivated instances under a hidden root and notifies <see cref="IPoolableView"/>
    /// components on rent/return. Does not reset instance state itself. Usually accessed via <see cref="ViewPoolRegistry"/>.
    /// </summary>
    public sealed class GameObjectPool
    {
        static readonly List<IPoolableView> _poolableBuffer = new();

        readonly GameObject _prefab;
        readonly Transform _root;
        readonly int _maxSize;
        readonly Stack<GameObject> _free = new();

        /// <summary>The prefab this pool spawns instances of.</summary>
        public GameObject Prefab => _prefab;
        /// <summary>The number of parked instances currently available to rent.</summary>
        public int FreeCount => _free.Count;
        /// <summary>The maximum number of parked instances kept; returns beyond this destroy instead.</summary>
        public int MaxSize => _maxSize;

        /// <summary>Creates a pool for <paramref name="prefab"/>, parking returns under <paramref name="root"/>, capped at <paramref name="maxSize"/> (min 1).</summary>
        public GameObjectPool(GameObject prefab, Transform root, int maxSize)
        {
            _prefab = prefab;
            _root = root;
            _maxSize = Mathf.Max(1, maxSize);
        }

        public void Prewarm(int count)
        {
            if (_prefab == null) return;
            for (int i = 0; i < count && _free.Count < _maxSize; i++)
            {
                var instance = UnityEngine.Object.Instantiate(_prefab, _root);
                Stamp(instance);
                instance.SetActive(false);
                _free.Push(instance);
            }
        }

        public GameObject Rent()
        {
            while (_free.Count > 0)
            {
                var pooled = _free.Pop();
                if (pooled == null) continue;
                pooled.transform.SetParent(null, false);
                pooled.SetActive(true);
                Notify(pooled, true);
                return pooled;
            }

            if (_prefab == null) return null;
            var created = UnityEngine.Object.Instantiate(_prefab);
            Stamp(created);
            Notify(created, true);
            return created;
        }

        public bool Return(GameObject instance)
        {
            if (instance == null) return false;

            if (_free.Count >= _maxSize)
            {
                UnityEngine.Object.Destroy(instance);
                return false;
            }

            Notify(instance, false);
            instance.SetActive(false);
            if (_root != null) instance.transform.SetParent(_root, false);
            _free.Push(instance);
            return true;
        }

        public void Clear()
        {
            while (_free.Count > 0)
            {
                var instance = _free.Pop();
                if (instance == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(instance);
                else UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        void Stamp(GameObject instance)
        {
            if (!instance.TryGetComponent<PooledView>(out var marker))
                marker = instance.AddComponent<PooledView>();
            marker.Owner = this;
        }

        static void Notify(GameObject instance, bool rent)
        {
            instance.GetComponentsInChildren(true, _poolableBuffer);
            for (int i = 0; i < _poolableBuffer.Count; i++)
            {
                try
                {
                    if (rent) _poolableBuffer[i].OnRent();
                    else _poolableBuffer[i].OnReturn();
                }
                catch (Exception ex)
                {
                    EosLog.Error($"{(rent ? "OnRent" : "OnReturn")} threw: {ex}", nameof(GameObjectPool));
                }
            }
            _poolableBuffer.Clear();
        }
    }
}
