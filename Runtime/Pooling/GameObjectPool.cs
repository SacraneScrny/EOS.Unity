using System;
using System.Collections.Generic;
using EOS.Logging;
using UnityEngine;

namespace EOS.Unity
{
    public sealed class GameObjectPool
    {
        static readonly List<IPoolableView> _poolableBuffer = new();

        readonly GameObject _prefab;
        readonly Transform _root;
        readonly int _maxSize;
        readonly Stack<GameObject> _free = new();

        public GameObject Prefab => _prefab;
        public int FreeCount => _free.Count;
        public int MaxSize => _maxSize;

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
