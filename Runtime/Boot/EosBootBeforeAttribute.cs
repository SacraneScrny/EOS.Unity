using System;

namespace EOS.Unity
{
    /// <summary>
    /// Orders the annotated boot step <em>before</em> another step. Without
    /// <see cref="Method"/> it means "before every boot method of <see cref="Type"/>";
    /// with <see cref="Method"/> it targets that single method (useful when a class has
    /// several boot steps). May be applied multiple times.
    /// </summary>
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
