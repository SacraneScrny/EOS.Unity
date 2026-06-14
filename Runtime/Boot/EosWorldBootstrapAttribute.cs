using System;

namespace EOS.Unity
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class EosWorldBootstrapAttribute : Attribute
    {
        public int Order { get; }

        public EosWorldBootstrapAttribute(int order = 0)
        {
            Order = order;
        }
    }
}
