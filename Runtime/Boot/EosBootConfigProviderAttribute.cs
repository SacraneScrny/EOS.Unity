using System;

namespace EOS.Unity
{
    /// <summary>Marks a <c>public static EosBootConfig(EosBootConfig)</c> method that contributes to the boot config; providers are threaded in order on the cold boot path.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class EosBootConfigProviderAttribute : Attribute
    {
        /// <summary>Sort priority among config providers; lower runs first.</summary>
        public int Order { get; }

        /// <summary>Marks the method as a boot-config provider with the given <paramref name="order"/>.</summary>
        public EosBootConfigProviderAttribute(int order = 0)
        {
            Order = order;
        }
    }
}
