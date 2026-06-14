using System;
using System.Collections.Generic;
using UnityEngine;

namespace EOS.Unity
{
    /// <summary>One named attachment point authored on a view prefab: an <see cref="Id"/>, a required module <see cref="Kind"/>, and the <see cref="Anchor"/> transform a module's view parents under.</summary>
    [Serializable]
    public sealed class Socket
    {
        /// <summary>The socket id, unique within a <see cref="SocketSet"/>; the key used by <see cref="AssemblyService.Attach(EosEntity, string, EosEntity)"/>.</summary>
        public string Id;
        /// <summary>The module kind name this socket accepts; empty means any kind is allowed.</summary>
        [ModuleKindField] public string Kind;
        /// <summary>The transform attached module views are parented under at bind time.</summary>
        public Transform Anchor;
    }

    /// <summary>MonoBehaviour authored on an incarnation prefab root declaring its sockets; resolved at runtime through the entity's view to anchor attached modules.</summary>
    [AddComponentMenu("Sackrany/EOS/Socket Set")]
    public sealed class SocketSet : MonoBehaviour
    {
        [SerializeField] Socket[] _sockets = Array.Empty<Socket>();

        /// <summary>The authored sockets on this prefab.</summary>
        public IReadOnlyList<Socket> Sockets => _sockets;

        /// <summary>Looks up a socket by id; returns false (and null) when the id is empty or absent.</summary>
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
