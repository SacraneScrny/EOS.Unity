using System;

namespace EOS.Unity
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class EosBootAttribute : Attribute
    {
        public int Order { get; }

        public bool IsFallback { get; }

        public EosBootAttribute(int order = 0, bool isFallback = false)
        {
            Order = order;
            IsFallback = isFallback;
        }
    }
}
