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
                EosLog.Warning($"view bind for socket '{link.SocketId}' skipped: parent or module is no longer valid", nameof(AssemblyViewBinder));
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

            var socketKind = ModuleKind.Of(socket.Kind);
            if (socketKind.IsValid)
            {
                if (!module.TryGet<Module>(out var m))
                {
                    EosLog.Error($"socket '{link.SocketId}' on '{parent.Name}' requires kind '{socket.Kind}' but module '{module.Name}' has no Module component", nameof(AssemblyViewBinder));
                    link.ViewBound = true;
                    module.DetachFromSocket();
                    return true;
                }
                if (m.Kind != socketKind)
                {
                    EosLog.Error($"module '{module.Name}' kind '{m.Kind}' mismatches socket '{link.SocketId}' (kind '{socket.Kind}') on '{parent.Name}'", nameof(AssemblyViewBinder));
                    link.ViewBound = true;
                    module.DetachFromSocket();
                    return true;
                }
            }

            var t = moduleGo.transform;
            t.SetParent(socket.Anchor, false);
            if (module.TryGet<EntityTransform>(out var offset))
            {
                t.localPosition = offset.LocalPosition;
                t.localRotation = offset.LocalRotation;
                t.localScale = offset.LocalScale;
            }
            else
            {
                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.identity;
            }
            link.ViewBound = true;
            return true;
        }

        public static void Unbind(EosEntity module)
        {
            var go = GetViewObject(module);
            if (go != null) go.transform.SetParent(null, true);
        }
    }
}
