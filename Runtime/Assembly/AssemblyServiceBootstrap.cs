using EOS.Core;

namespace EOS.Unity
{
    // Registers the per-world AssemblyService eagerly on every world (default and created later)
    // through the world-bootstrap codegen. Replaces the old lazy-on-first-access registration;
    // AssemblyExtensions.Assemblies keeps a lazy fallback for when the codegen file is not generated.
    public static class AssemblyServiceBootstrap
    {
        [EosWorldBootstrap]
        public static void Register(World world)
            => world.ServiceRegistry.Register(new AssemblyService(world));
    }
}
