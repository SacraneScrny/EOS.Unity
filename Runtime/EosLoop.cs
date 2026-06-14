using EOS.Core;
using EOS.Profiling;

namespace EOS.Unity
{
    /// <summary>Explicit, idempotent entry point that boots the EOS runtime: applies config, loads incarnations, registers binders, boots the <c>Universe</c>, and installs the PlayerLoop hooks.</summary>
    public static class EosLoop
    {
        /// <summary>True once <see cref="Boot"/> has run and before <see cref="Shutdown"/>; guards re-boot and gates spawners.</summary>
        public static bool IsBooted { get; private set; }

        /// <summary>Boots the runtime from the given config (default if null); no-op if already booted.</summary>
        public static void Boot(EosBootConfig config = null)
        {
            if (IsBooted) return;
            
            config ??= new EosBootConfig();

            UnityLogHandler.MinLevel = config.MinLogLevel;

            if (config.EnableProfiler)
            {
                EosProfiler.Backend = new UnityProfilerBackend();
                EosProfiler.Enabled = true;
            }
            else
            {
                EosProfiler.Backend = NullProfilerBackend.Instance;
                EosProfiler.Enabled = false;
            }

            IncarnationDatabase.Load();
            config.ApplyBinders();

            Universe.Boot();
            EosPlayerLoop.Install();

            if (config.DebugDraw)
                EosDebugDrawer.Ensure();

            IsBooted = true;
        }

        /// <summary>Reverses Boot: uninstalls the PlayerLoop, removes the debug drawer, shuts down the <c>Universe</c>, clears view pools, and unloads incarnations; no-op if not booted.</summary>
        public static void Shutdown()
        {
            if (!IsBooted) return;
            EosPlayerLoop.Uninstall();
            EosDebugDrawer.Remove();
            Universe.Shutdown();
            ViewPoolRegistry.ClearAll();
            IncarnationDatabase.Unload();
            IsBooted = false;
        }
    }
}
