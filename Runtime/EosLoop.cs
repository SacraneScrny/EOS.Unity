using EOS.Core;
using EOS.Profiling;

namespace EOS.Unity
{
    public static class EosLoop
    {
        public static bool IsBooted { get; private set; }

        public static void Boot(EosBootConfig config = null)
        {
            config ??= new EosBootConfig();

            UnityLogHandler.MinLevel = config.MinLogLevel;

            if (config.EnableProfiler)
            {
                EosProfiler.Backend = new UnityProfilerBackend();
                EosProfiler.Enabled = true;
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
            EosPlayerLoop.Uninstall();
            EosDebugDrawer.Remove();
            Universe.Off();
            IsBooted = false;
        }
    }
}
