using System;
using System.Collections.Generic;
using EOS.Loader;
using EOS.Logging;

namespace EOS.Unity
{
    /// <summary>Serializable boot options passed to <see cref="EosLoop.Boot"/>; configures profiling, debug draw, log level, and deferred binder registrations.</summary>
    [Serializable]
    public sealed class EosBootConfig
    {
        /// <summary>When true, installs the Unity profiler backend and enables profiling; false uses the null backend.</summary>
        public bool EnableProfiler;
        /// <summary>When true, spawns the <see cref="EosDebugDrawer"/> so gizmo debug draw runs in play mode.</summary>
        public bool DebugDraw = true;
        /// <summary>Minimum log level forwarded to the Unity console by <see cref="UnityLogHandler"/>.</summary>
        public LogLevel MinLogLevel = LogLevel.Debug;

        readonly List<Action> _binders = new();

        /// <summary>Queues an incarnation binder to register during Boot; chainable.</summary>
        public EosBootConfig AddBinder<TView>(IIncarnationBinder<TView> binder) where TView : class
        {
            _binders.Add(() => IncarnationBridge.Register(binder));
            return this;
        }

        internal void ApplyBinders()
        {
            foreach (var register in _binders)
            {
                try { register(); }
                catch (Exception ex) { EosLog.Error($"binder registration threw: {ex.Message}", nameof(EosBootConfig)); }
            }
        }
    }
}
