using System;
using System.Collections.Generic;
using EOS.Loader;
using EOS.Logging;

namespace EOS.Unity
{
    public sealed class EosBootConfig
    {
        public bool EnableProfiler;
        public bool DebugDraw = true;
        public LogLevel MinLogLevel = LogLevel.Debug;

        readonly List<Action> _binders = new();

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
