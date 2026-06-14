using System;

namespace EOS.Unity
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class EosBootConfigProviderAttribute : Attribute
    {
        public int Order { get; }

        public EosBootConfigProviderAttribute(int order = 0)
        {
            Order = order;
        }
    }
}
