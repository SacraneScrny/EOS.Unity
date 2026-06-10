using EOS.Core;
using EOS.Profiling;

namespace EOS.Unity
{
    public static class EosLoop
    {
        public static bool IsBooted { get; private set; }

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
