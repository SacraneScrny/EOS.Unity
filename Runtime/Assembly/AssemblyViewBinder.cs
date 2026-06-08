using EOS.Entities;
using EOS.Extensions;
using EOS.Logging;
using EOS.Objects;
using UnityEngine;

namespace EOS.Unity
{
    internal static class AssemblyViewBinder
    {
        public static GameObject GetViewObject(EosEntity entity)
        {
            if (!entity.IsValid) return null;

            if (entity.TryGet<Incarnation<EntityIncarnation>>(out var view) && view.View != null)
                return view.View.gameObject;

            if (entity.TryGet<Incarnation<GameObject>>(out var go) && go.View != null)
                return go.View;

            return null;
        }

        public static bool TryBind(EosEntity module, AttachedTo link)
        {
            var parent = link.Parent;
            if (!parent.IsValid || !module.IsValid)
            {
                link.ViewBound = true;
                return true;
            }

            var parentGo = GetViewObject(parent);
            var moduleGo = GetViewObject(module);
            if (parentGo == null || moduleGo == null) return false;

            var set = parentGo.GetComponent<SocketSet>();
            if (set == null || !set.TryGet(link.SocketId, out var socket) || socket.Anchor == null)
            {
                EosLog.Error($"socket '{link.SocketId}' not found on '{parent.Name}'", nameof(AssemblyViewBinder));
                link.ViewBound = true;
                return true;
            }

            if (KindMismatch(module, socket))
            {
                EosLog.Error($"module kind mismatches socket '{link.SocketId}' (kind '{socket.Kind}') on '{parent.Name}'", nameof(AssemblyViewBinder));
                link.ViewBound = true;
                module.Detach();
                return true;
            }

            var t = moduleGo.transform;
            t.SetParent(socket.Anchor, false);
            t.localPosition = link.LocalPosition;
            t.localRotation = link.LocalRotation;
            link.ViewBound = true;
            return true;
        }

        public static void Unbind(EosEntity module)
        {
            var go = GetViewObject(module);
            if (go != null) go.transform.SetParent(null, true);
        }

        static bool KindMismatch(EosEntity module, Socket socket)
        {
            var socketKind = ModuleKind.Of(socket.Kind);
            if (!socketKind.IsValid) return false;
            if (!module.TryGet<Module>(out var m)) return true;
            return m.Kind != socketKind;
        }
    }
}
