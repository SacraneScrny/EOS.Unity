using System;

namespace EOS.Unity
{
    /// <summary>
    /// Orders the annotated boot step <em>after</em> another step. Without
    /// <see cref="Method"/> it means "after every boot method of <see cref="Type"/>";
    /// with <see cref="Method"/> it targets that single method (useful when a class has
    /// several boot steps). May be applied multiple times.
    /// </summary>
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
