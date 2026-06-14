using System;
using System.Collections.Generic;
using EOS.Core;
using EOS.Entities;
using EOS.Extensions;
using EOS.Logging;
using UnityEngine;

namespace EOS.Unity
{
    /// <summary>Per-world service managing the typed socket/module assembly graph; attaches, detaches and queries modules, deferring structural changes while iterating.</summary>
    public sealed class AssemblyService
    {
        readonly World _world;

        /// <summary>Creates the service bound to the given world.</summary>
        public AssemblyService(World world) => _world = world;

        /// <summary>Attaches <paramref name="module"/> into the given socket on <paramref name="parent"/>, leaving any existing offset untouched; deferred when iterating.</summary>
        public bool Attach(EosEntity parent, string socketId, EosEntity module)
        {
            if (!Validate(parent, socketId, module)) return false;

            if (_world.IsIterating)
            {
                Defer(module, () => AttachCore(parent, socketId, module, false, Vector3.zero, Quaternion.identity));
                return true;
            }

            return AttachCore(parent, socketId, module, false, Vector3.zero, Quaternion.identity);
        }

        /// <summary>Attaches <paramref name="module"/> into the socket on <paramref name="parent"/> and writes the given local position/rotation; deferred when iterating.</summary>
        public bool Attach(EosEntity parent, string socketId, EosEntity module, Vector3 localPosition, Quaternion localRotation)
        {
            if (!Validate(parent, socketId, module)) return false;

            if (_world.IsIterating)
            {
                Defer(module, () => AttachCore(parent, socketId, module, true, localPosition, localRotation));
                return true;
            }

            return AttachCore(parent, socketId, module, true, localPosition, localRotation);
        }

        /// <summary>Detaches <paramref name="module"/> from its socket and clears the native parent if it still points at the assembly parent; deferred when iterating.</summary>
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

        /// <summary>Sets the module's local position/rotation offset via its <see cref="EntityTransform"/>; deferred when iterating and the transform must be added.</summary>
        public bool SetLocalOffset(EosEntity module, Vector3 localPosition, Quaternion localRotation)
        {
            if (!module.IsValid || !module.Has<AttachedTo>()) return false;

            if (_world.IsIterating && !module.Has<EntityTransform>())
            {
                Defer(module, () => SetOffsetCore(module, localPosition, localRotation));
                return true;
            }

            return SetOffsetCore(module, localPosition, localRotation);
        }

        /// <summary>Gets the module currently held in the given socket on <paramref name="parent"/>, if any.</summary>
        public bool TryGetModule(EosEntity parent, string socketId, out EosEntity module)
        {
            module = EosEntity.Null;
            return parent.IsValid
                && parent.TryGet<EntityAssembly>(out var asm)
                && asm.TryGetModule(socketId, out module);
        }

        /// <summary>Collects all modules held by <paramref name="parent"/> into <paramref name="into"/> and returns the count.</summary>
        public int GetModules(EosEntity parent, List<EosEntity> into)
            => parent.IsValid && parent.TryGet<EntityAssembly>(out var asm) ? asm.Collect(into) : 0;

        /// <summary>Returns true when the given socket on <paramref name="parent"/> currently holds no module.</summary>
        public bool IsSocketFree(EosEntity parent, string socketId)
            => !TryGetModule(parent, socketId, out _);

        bool Validate(EosEntity parent, string socketId, EosEntity module)
        {
            if (!parent.IsValid) { EosLog.Error("attach: parent is invalid", nameof(AssemblyService)); return false; }
            if (!module.IsValid) { EosLog.Error("attach: module is invalid", nameof(AssemblyService)); return false; }
            if (string.IsNullOrEmpty(socketId)) { EosLog.Error("attach: socketId is empty", nameof(AssemblyService)); return false; }
            if (parent == module) { EosLog.Error("attach: cannot attach an entity to itself", nameof(AssemblyService)); return false; }
            if (module.Has<AttachedTo>()) { EosLog.Error("attach: module is already attached", nameof(AssemblyService)); return false; }
            if (_world.Hierarchy.IsDescendantOf(parent, module)) { EosLog.Error("attach: attaching would create a hierarchy cycle", nameof(AssemblyService)); return false; }
            if (!IsSocketFree(parent, socketId)) { EosLog.Error($"attach: socket '{socketId}' is occupied", nameof(AssemblyService)); return false; }
            return true;
        }

        bool AttachCore(EosEntity parent, string socketId, EosEntity module, bool applyOffset, Vector3 localPosition, Quaternion localRotation)
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
            if (_world.Hierarchy.IsDescendantOf(parent, module))
            {
                EosLog.Warning($"deferred attach to socket '{socketId}' dropped: attaching would create a hierarchy cycle", nameof(AssemblyService));
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
            link.ViewBound = false;

            _world.Hierarchy.SetParent(module, parent);

            if (applyOffset)
                SetOffsetCore(module, localPosition, localRotation);

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

            if (_world.Hierarchy.GetParent(module) == parent)
                _world.Hierarchy.SetParent(module, EosEntity.Null);

            _world.Event(new ModuleDetached(parent, module, socketId));
            return true;
        }

        bool SetOffsetCore(EosEntity module, Vector3 localPosition, Quaternion localRotation)
        {
            if (!module.IsValid)
            {
                EosLog.Warning("deferred offset dropped: module is no longer valid", nameof(AssemblyService));
                return false;
            }

            var transform = module.Has<EntityTransform>() ? module.Get<EntityTransform>() : module.Add<EntityTransform>();
            transform.LocalPosition = localPosition;
            transform.LocalRotation = localRotation;
            return true;
        }

        void Defer(EosEntity anchor, Action action)
            => _world.AfterCurrentPhase.Schedule(anchor).If(_ => { action(); return true; });
    }
}
