using EOS.Core;
using EOS.Entities;
using UnityEngine;

namespace EOS.Unity
{
    public static class AssemblyExtensions
    {
        public static AssemblyService Assemblies(this World world)
        {
            if (world == null) return null;
            if (world.Services.TryGet<AssemblyService>(out var service)) return service;

            service = new AssemblyService(world);
            if (!world.IsIterating)
                world.ServiceRegistry.Register(service);

            return service;
        }

        public static bool AttachTo(this EosEntity module, EosEntity parent, string socketId)
            => module.World != null && module.World.Assemblies().Attach(parent, socketId, module);

        public static bool AttachTo(this EosEntity module, EosEntity parent, string socketId, Vector3 localPosition, Quaternion localRotation)
            => module.World != null && module.World.Assemblies().Attach(parent, socketId, module, localPosition, localRotation);

        public static bool DetachFromSocket(this EosEntity module)
            => module.World != null && module.World.Assemblies().Detach(module);

        public static bool TryGetModule(this EosEntity parent, string socketId, out EosEntity module)
        {
            module = EosEntity.Null;
            return parent.World != null && parent.World.Assemblies().TryGetModule(parent, socketId, out module);
        }
    }
}
