using System;
using System.Collections.Generic;
using EOS.Core;
using EOS.Entities;
using EOS.Extensions;
using EOS.Logging;
using UnityEngine;

namespace EOS.Unity
{
    public sealed class AssemblyService
    {
        readonly World _world;

        public AssemblyService(World world) => _world = world;

        public bool Attach(EosEntity parent, string socketId, EosEntity module)
            => Attach(parent, socketId, module, Vector3.zero, Quaternion.identity);

        public bool Attach(EosEntity parent, string socketId, EosEntity module, Vector3 localPosition, Quaternion localRotation)
        {
            if (!Validate(parent, socketId, module)) return false;

            if (_world.IsIterating)
            {
                Defer(module, () => AttachCore(parent, socketId, module, localPosition, localRotation));
                return true;
            }

            return AttachCore(parent, socketId, module, localPosition, localRotation);
        }

        public bool Detach(EosEntity module)
        {
            if (!module.IsValid || !module.Has<AttachedTo>()) return false;

            if (_world.IsIterating)
            {
                Defer(module, () => DetachCore(module));
                return true;
            }

            return DetachCore(module);
        }

        public bool SetLocalOffset(EosEntity module, Vector3 localPosition, Quaternion localRotation)
        {
            if (!module.IsValid || !module.TryGet<AttachedTo>(out var link)) return false;

            link.LocalPosition = localPosition;
            link.LocalRotation = localRotation;

            var go = AssemblyViewBinder.GetViewObject(module);
            if (go != null && go.transform.parent != null)
            {
                go.transform.localPosition = localPosition;
                go.transform.localRotation = localRotation;
            }

            return true;
        }

        public bool TryGetModule(EosEntity parent, string socketId, out EosEntity module)
        {
            module = EosEntity.Null;
            return parent.IsValid
                && parent.TryGet<EntityAssembly>(out var asm)
                && asm.TryGetModule(socketId, out module);
        }

        public int GetModules(EosEntity parent, List<EosEntity> into)
            => parent.IsValid && parent.TryGet<EntityAssembly>(out var asm) ? asm.Collect(into) : 0;

        public bool IsSocketFree(EosEntity parent, string socketId)
            => !TryGetModule(parent, socketId, out _);

        bool Validate(EosEntity parent, string socketId, EosEntity module)
        {
            if (!parent.IsValid) { EosLog.Error("attach: parent is invalid", nameof(AssemblyService)); return false; }
            if (!module.IsValid) { EosLog.Error("attach: module is invalid", nameof(AssemblyService)); return false; }
            if (string.IsNullOrEmpty(socketId)) { EosLog.Error("attach: socketId is empty", nameof(AssemblyService)); return false; }
            if (parent == module) { EosLog.Error("attach: cannot attach an entity to itself", nameof(AssemblyService)); return false; }
            if (module.Has<AttachedTo>()) { EosLog.Error("attach: module is already attached", nameof(AssemblyService)); return false; }
            if (!IsSocketFree(parent, socketId)) { EosLog.Error($"attach: socket '{socketId}' is occupied", nameof(AssemblyService)); return false; }
            return true;
        }

        bool AttachCore(EosEntity parent, string socketId, EosEntity module, Vector3 localPosition, Quaternion localRotation)
        {
            if (!parent.IsValid || !module.IsValid)
            {
                EosLog.Warning($"deferred attach to socket '{socketId}' dropped: parent or module is no longer valid", nameof(AssemblyService));
                return false;
            }
            if (module.Has<AttachedTo>())
            {
                EosLog.Warning($"deferred attach to socket '{socketId}' dropped: module is already attached", nameof(AssemblyService));
                return false;
            }
            if (!IsSocketFree(parent, socketId))
            {
                EosLog.Warning($"deferred attach dropped: socket '{socketId}' is occupied", nameof(AssemblyService));
                return false;
            }

            var asm = parent.Has<EntityAssembly>() ? parent.Get<EntityAssembly>() : parent.Add<EntityAssembly>();
            asm.Hold(socketId, module);

            var link = module.Add<AttachedTo>();
            link.Parent = parent;
            link.SocketId = socketId;
            link.LocalPosition = localPosition;
            link.LocalRotation = localRotation;
            link.ViewBound = false;

            AssemblyViewBinder.TryBind(module, link);

            _world.Event(new ModuleAttached(parent, module, socketId));
            return true;
        }

        internal void NotifyDetachedOnDispose(EosEntity parent, EosEntity module, string socketId)
            => _world.Event(new ModuleDetached(parent, module, socketId));

        bool DetachCore(EosEntity module)
        {
            if (!module.IsValid || !module.TryGet<AttachedTo>(out var link))
            {
                EosLog.Warning("deferred detach dropped: module is no longer valid or not attached", nameof(AssemblyService));
                return false;
            }

            var parent = link.Parent;
            var socketId = link.SocketId;
            link.Detaching = true;

            if (parent.IsValid && parent.TryGet<EntityAssembly>(out var asm))
                asm.ReleaseIfHolds(socketId, module);

            AssemblyViewBinder.Unbind(module);
            module.Remove<AttachedTo>();

            _world.Event(new ModuleDetached(parent, module, socketId));
            return true;
        }

        void Defer(EosEntity anchor, Action action)
            => _world.AfterCurrentPhase.Schedule(anchor).If(_ => { action(); return true; });
    }
}
