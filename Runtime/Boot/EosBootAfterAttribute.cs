using System;

namespace EOS.Unity
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class EosBootAfterAttribute : Attribute
    {
        public Type Type { get; }
        public string Method { get; }

        public EosBootAfterAttribute(Type type, string method = null)
        {
            Type = type;
            Method = method;
        }
    }
}
