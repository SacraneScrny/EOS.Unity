using System.Runtime.CompilerServices;
using EOS.Core;
using EOS.Entities;
using UnityEngine;

namespace EOS.Unity
{
    public static class AssemblyExtensions
    {
        static readonly ConditionalWeakTable<World, AssemblyService> _services = new();

        public static AssemblyService Assemblies(this World world)
        {
            if (world == null) return null;
            if (_services.TryGetValue(world, out var service)) return service;

            service = new AssemblyService(world);
            _services.Add(world, service);

            if (!world.IsIterating && !world.ServiceRegistry.Has<AssemblyService>())
                world.ServiceRegistry.Register(service);

            return service;
        }

        public static bool AttachTo(this EosEntity module, EosEntity parent, string socketId)
            => module.World != null && module.World.Assemblies().Attach(parent, socketId, module);

        public static bool AttachTo(this EosEntity module, EosEntity parent, string socketId, Vector3 localPosition, Quaternion localRotation)
            => module.World != null && module.World.Assemblies().Attach(parent, socketId, module, localPosition, localRotation);

        public static bool Detach(this EosEntity module)
            => module.World != null && module.World.Assemblies().Detach(module);

        public static bool TryGetModule(this EosEntity parent, string socketId, out EosEntity module)
        {
            module = EosEntity.Null;
            return parent.World != null && parent.World.Assemblies().TryGetModule(parent, socketId, out module);
        }
    }
}
