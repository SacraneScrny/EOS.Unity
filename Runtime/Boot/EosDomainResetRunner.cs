using System;
using System.Collections.Generic;
using System.Reflection;
using EOS.Logging;

namespace EOS.Unity
{
    /// <summary>Discovers and invokes all <see cref="EosDomainResetAttribute"/> methods via reflection, caching the delegates per domain.</summary>
    public static class EosDomainResetRunner
    {
        const string Tag = nameof(EosDomainResetRunner);
        static Action[] _resets;

        /// <summary>Invokes every discovered domain-reset method (each try/caught); discovers and caches them on first call.</summary>
        public static void Run()
        {
            var resets = _resets ??= Discover();
            for (int i = 0; i < resets.Length; i++)
            {
                try { resets[i](); }
                catch (Exception ex) { EosLog.Error($"domain reset threw: {ex.Message}", Tag); }
            }
        }

        static Action[] Discover()
        {
            var list = new List<Action>();

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (Skip(asm)) continue;

                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types; }
                catch { continue; }

                foreach (var type in types)
                {
                    if (type == null) continue;

                    const BindingFlags flags = BindingFlags.Static | BindingFlags.Public
                                             | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

                    foreach (var method in type.GetMethods(flags))
                    {
                        if (method.GetCustomAttribute<EosDomainResetAttribute>() == null) continue;

                        if (!method.IsStatic || method.GetParameters().Length != 0
                            || method.ReturnType != typeof(void)
                            || method.IsGenericMethodDefinition || type.IsGenericTypeDefinition)
                        {
                            EosLog.Warning(
                                $"[EosDomainReset] '{type.Name}.{method.Name}' must be a static parameterless void method; skipped",
                                Tag);
                            continue;
                        }

                        try { list.Add((Action)Delegate.CreateDelegate(typeof(Action), method)); }
                        catch (Exception ex)
                        {
                            EosLog.Error($"bind domain reset '{type.Name}.{method.Name}' threw: {ex.Message}", Tag);
                        }
                    }
                }
            }

            return list.ToArray();
        }

        static bool Skip(Assembly asm)
        {
            var name = asm.GetName().Name;
            return name.StartsWith("System.", StringComparison.Ordinal)
                || name.StartsWith("Unity.", StringComparison.Ordinal)
                || name.StartsWith("mscorlib", StringComparison.Ordinal)
                || name.StartsWith("netstandard", StringComparison.Ordinal)
                || name.StartsWith("Mono.", StringComparison.Ordinal);
        }
    }
}
