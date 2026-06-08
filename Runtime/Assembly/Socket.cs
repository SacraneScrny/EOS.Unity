using System;
using System.Collections.Generic;
using UnityEngine;

namespace EOS.Unity
{
    [Serializable]
    public sealed class Socket
    {
        public string Id;
        public string Kind;
        public Transform Anchor;
    }

    [AddComponentMenu("Sackrany/EOS/Socket Set")]
    public sealed class SocketSet : MonoBehaviour
    {
        [SerializeField] Socket[] _sockets = Array.Empty<Socket>();

        public IReadOnlyList<Socket> Sockets => _sockets;

        public bool TryGet(string id, out Socket socket)
        {
            if (!string.IsNullOrEmpty(id) && _sockets != null)
            {
                for (int i = 0; i < _sockets.Length; i++)
                {
                    var s = _sockets[i];
                    if (s != null && s.Id == id)
                    {
                        socket = s;
                        return true;
                    }
                }
            }

            socket = null;
            return false;
        }

        void OnDrawGizmos()
        {
            if (_sockets == null) return;

            for (int i = 0; i < _sockets.Length; i++)
            {
                var s = _sockets[i];
                if (s == null || s.Anchor == null) continue;

                var t = s.Anchor;

                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(t.position, 0.025f);

                Gizmos.color = Color.blue;
                Gizmos.DrawLine(t.position, t.position + t.forward * 0.2f);

                Gizmos.color = Color.green;
                Gizmos.DrawLine(t.position, t.position + t.up * 0.2f);

                Gizmos.color = Color.red;
                Gizmos.DrawLine(t.position, t.position + t.right * 0.2f);

                #if UNITY_EDITOR
                UnityEditor.Handles.color = Color.white;
                UnityEditor.Handles.Label(t.position + Vector3.up * 0.05f, $"[{s.Id}] {s.Kind}");
                #endif
            }
        }
    }
}
