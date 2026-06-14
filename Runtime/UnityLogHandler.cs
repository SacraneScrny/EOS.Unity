using EOS.Logging;
using UnityEngine;

namespace EOS.Unity
{
    /// <summary>Routes <c>EosLog</c> output to the Unity console as <c>[EOS:source] message</c>, mapping levels to <c>Debug.Log/LogWarning/LogError</c>.</summary>
    public static class UnityLogHandler
    {
        /// <summary>Minimum level forwarded to the console; entries below it are dropped before formatting.</summary>
        public static LogLevel MinLevel = LogLevel.Debug;

        /// <summary>Hooks this handler into <c>EosLog.OnLog</c>.</summary>
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
