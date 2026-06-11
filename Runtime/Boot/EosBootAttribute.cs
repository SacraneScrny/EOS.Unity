using System;

namespace EOS.Unity
{
    /// <summary>
    /// Marks a <c>public static</c> parameterless method as a boot step. The editor
    /// collects every such method across the project, sorts them (see
    /// <see cref="EosBootBeforeAttribute"/> / <see cref="EosBootAfterAttribute"/> and
    /// <see cref="Order"/>) and bakes the call order into a generated bootstrap that
    /// runs on <c>RuntimeInitializeOnLoadMethod(BeforeSceneLoad)</c>.
    /// </summary>
    /// <remarks>
    /// The method must be <c>static</c>, <c>public</c> and take no parameters; other
    /// shapes are skipped with a warning so the generated file always compiles.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class EosBootAttribute : Attribute
    {
        /// <summary>Coarse ordering key; lower runs earlier. Ties broken deterministically by type/method name.</summary>
        public int Order { get; }

        /// <summary>
        /// When true the step also runs on the "warm" path — when EOS is already booted
        /// and the bootstrap is invoked again, only fallback steps run (the built-in
        /// <c>EosLoop.Boot()</c> and non-fallback steps are skipped).
        /// </summary>
        public bool IsFallback { get; }

        public EosBootAttribute(int order = 0, bool isFallback = false)
        {
            Order = order;
            IsFallback = isFallback;
        }
    }
}
