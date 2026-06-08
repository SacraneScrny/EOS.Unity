using System;
using System.Collections.Generic;
using EOS.Core;
using EOS.Entities;
using EOS.Logging;

namespace EOS.Unity
{
    [Serializable]
    public sealed class DefaultModule
    {
        public string SocketId;
        public EntityPreset Module;
    }

    internal static class AssemblyDefaults
    {
        [ThreadStatic] static int _depth;
        const int MaxDepth = 32;

        public static void Apply(World world, EosEntity root, IReadOnlyList<DefaultModule> defaults)
        {
            if (world == null || root.IsValid == false) return;
            if (defaults == null || defaults.Count == 0) return;

            if (_depth >= MaxDepth)
            {
                EosLog.Error("default modules nested too deep; aborting (cycle?)", nameof(AssemblyDefaults));
                return;
            }

            _depth++;
            try
            {
                for (int i = 0; i < defaults.Count; i++)
                {
                    var def = defaults[i];
                    if (def == null || def.Module == null || string.IsNullOrEmpty(def.SocketId)) continue;

                    EosEntity module;
                    try { module = def.Module.Instantiate(world); }
                    catch (Exception ex)
                    {
                        EosLog.Error($"default module '{def.Module.name}' spawn threw: {ex.Message}", nameof(AssemblyDefaults));
                        continue;
                    }

                    if (!module.IsValid) continue;

                    if (!module.AttachTo(root, def.SocketId))
                    {
                        EosLog.Warning($"default module '{def.Module.name}' could not attach to socket '{def.SocketId}'", nameof(AssemblyDefaults));
                        module.Destroy();
                    }
                }
            }
            finally
            {
                _depth--;
            }
        }
    }
}
