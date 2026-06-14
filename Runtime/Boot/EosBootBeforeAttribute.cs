using System;

namespace EOS.Unity
{
    /// <summary>Declares that this boot step must run before the referenced step; an ordering edge in the boot topological sort.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class EosBootBeforeAttribute : Attribute
    {
        /// <summary>The type declaring the boot step that must run after this one.</summary>
        public Type Type { get; }
        /// <summary>Optional method name on <see cref="Type"/>; null targets all boot steps on the type.</summary>
        public string Method { get; }

        /// <summary>Declares this step must run before the step identified by <paramref name="type"/> and optional <paramref name="method"/>.</summary>
        public EosBootBeforeAttribute(Type type, string method = null)
        {
            Type = type;
            Method = method;
        }
    }
}
