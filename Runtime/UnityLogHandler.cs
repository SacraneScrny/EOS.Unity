using EOS.Logging;
using UnityEngine;

namespace EOS.Unity
{
    public static class UnityLogHandler
    {
        public static LogLevel MinLevel = LogLevel.Debug;

        public static void Install()
        {
            EosLog.OnLog = Handle;
        }

        static void Handle(LogEntry entry)
        {
            if (entry.Level < MinLevel) return;

            var message = entry.Source != null
                ? $"[EOS:{entry.Source}] {entry.Message}"
                : $"[EOS] {entry.Message}";

            switch (entry.Level)
            {
                case LogLevel.Error: Debug.LogError(message); break;
                case LogLevel.Warning: Debug.LogWarning(message); break;
                default: Debug.Log(message); break;
            }
        }
    }
}
