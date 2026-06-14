using EOS.Core;
using EOS.Entities;
using UnityEngine;

namespace EOS.Unity
{
    /// <summary>Entity-facing convenience wrappers over the world's <see cref="AssemblyService"/> for attaching, detaching and querying socket modules.</summary>
    public static class AssemblyExtensions
    {
        /// <summary>Returns the world's <see cref="AssemblyService"/>, lazily creating and registering one if absent (registration skipped while iterating).</summary>
        public static AssemblyService Assemblies(this World world)
        {
            if (world == null) return null;
            if (world.Services.TryGet<AssemblyService>(out var service)) return service;

            service = new AssemblyService(world);
            if (!world.IsIterating)
                world.ServiceRegistry.Register(service);

            return service;
        }

        /// <summary>Attaches this module entity into the given socket on <paramref name="parent"/>, leaving any existing local offset untouched.</summary>
        public static bool AttachTo(this EosEntity module, EosEntity parent, string socketId)
            => module.World != null && module.World.Assemblies().Attach(parent, socketId, module);

        /// <summary>Attaches this module into the socket on <paramref name="parent"/> and writes the given local position/rotation offset.</summary>
        public static bool AttachTo(this EosEntity module, EosEntity parent, string socketId, Vector3 localPosition, Quaternion localRotation)
            => module.World != null && module.World.Assemblies().Attach(parent, socketId, module, localPosition, localRotation);

        /// <summary>Detaches this module from its socket, releasing both the assembly link and the native hierarchy parent.</summary>
        public static bool DetachFromSocket(this EosEntity module)
            => module.World != null && module.World.Assemblies().Detach(module);

        /// <summary>Gets the module currently held in the given socket on this parent entity, if any.</summary>
        public static bool TryGetModule(this EosEntity parent, string socketId, out EosEntity module)
        {
            module = EosEntity.Null;
            return parent.World != null && parent.World.Assemblies().TryGetModule(parent, socketId, out module);
        }
    }
}
