using EOS.Core;

namespace EOS.Unity
{
    /// <summary>World bootstrap that registers an <see cref="AssemblyService"/> into each world on init and reset.</summary>
    public static class AssemblyServiceBootstrap
    {
        /// <summary>Registers a fresh <see cref="AssemblyService"/> for the given world; invoked per-world by the generated bootstrap.</summary>
        [EosWorldBootstrap]
        public static void Register(World world)
            => world.ServiceRegistry.Register(new AssemblyService(world));
    }
}
