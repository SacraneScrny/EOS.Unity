using System;

namespace EOS.Unity
{
    /// <summary>Marks a <c>public static void()</c> method as a boot step, run once per session from the generated auto bootstrap.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class EosBootAttribute : Attribute
    {
        /// <summary>Tie-break priority after the Before/After ordering; lower runs first.</summary>
        public int Order { get; }

        /// <summary>When true, the step also runs on the warm path (already booted) instead of only on cold boot.</summary>
        public bool IsFallback { get; }

        /// <summary>Marks the method as a boot step with the given <paramref name="order"/> and <paramref name="isFallback"/> behavior.</summary>
        public EosBootAttribute(int order = 0, bool isFallback = false)
        {
            Order = order;
            IsFallback = isFallback;
        }
    }
}
