using EOS.Loader;
using UnityEngine;

namespace EOS.Unity
{
    /// <summary>Runs automatically every play session at <c>SubsystemRegistration</c> to reset core statics, install the log handler, run <c>[EosDomainReset]</c> methods, register default binders, and shut down on quit.</summary>
    public static class EosRuntimeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void OnSubsystemRegistration()
        {
            EosDomainReset.Reset();
            UnityLogHandler.Install();
            EosDomainResetRunner.Run();

            IncarnationBridge.Register(new GameObjectBinder());
            IncarnationBridge.Register(new EntityIncarnationBinder());

            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        static void OnQuitting()
        {
            if (EosLoop.IsBooted) EosLoop.Shutdown();
        }
    }
}
