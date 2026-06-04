using EOS.Loader;
using UnityEngine;

namespace EOS.Unity
{
    public static class EosRuntimeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void OnSubsystemRegistration()
        {
            EosDomainReset.Reset();
            UnityLogHandler.Install();

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
