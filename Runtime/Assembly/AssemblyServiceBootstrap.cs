using EOS.Core;

namespace EOS.Unity
{
    public static class AssemblyServiceBootstrap
    {
        [EosWorldBootstrap]
        public static void Register(World world)
            => world.ServiceRegistry.Register(new AssemblyService(world));
    }
}
