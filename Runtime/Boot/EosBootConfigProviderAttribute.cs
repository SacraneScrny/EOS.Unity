using System;

namespace EOS.Unity
{
    /// <summary>
    /// Marks a <c>public static</c> method that takes and returns an
    /// <see cref="EosBootConfig"/>, letting any system mutate the boot config before
    /// the built-in <c>EosLoop.Boot(config)</c> runs. Providers form a cooperative
    /// chain: the editor collects them, sorts them (<see cref="Order"/>, plus
    /// <see cref="EosBootBeforeAttribute"/> / <see cref="EosBootAfterAttribute"/>
    /// resolved among providers) and threads one config instance through each — the
    /// returned value flows into the next provider, then into boot.
    /// </summary>
    /// <remarks>
    /// Signature must be <c>public static EosBootConfig Name(EosBootConfig config)</c>
    /// on a public non-generic type; other shapes are skipped with a warning. Returning
    /// <c>null</c> keeps the incoming config. Runs only on the cold boot path.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class EosBootConfigProviderAttribute : Attribute
    {
        /// <summary>Coarse ordering key; lower runs earlier. Ties broken deterministically by type/method name.</summary>
        public int Order { get; }

        public EosBootConfigProviderAttribute(int order = 0)
        {
            Order = order;
        }
    }
}
