using System;

namespace EOS.Unity
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class EosBootBeforeAttribute : Attribute
    {
        public Type Type { get; }
        public string Method { get; }

        public EosBootBeforeAttribute(Type type, string method = null)
        {
            Type = type;
            Method = method;
        }
    }
}
