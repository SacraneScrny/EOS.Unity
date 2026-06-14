using System;

namespace EOS.Unity
{
    /// <summary>Adds an ordering edge so the decorated <c>[EosBoot]</c> step runs after the named step during boot-codegen's topological sort.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class EosBootAfterAttribute : Attribute
    {
        /// <summary>The type declaring the boot step to run before this one.</summary>
        public Type Type { get; }
        /// <summary>Optional method name on <see cref="Type"/>; null matches that type's boot step regardless of name.</summary>
        public string Method { get; }

        /// <summary>Declares that this step must run after the given type's boot method (or any of its boot methods when <paramref name="method"/> is null).</summary>
        public EosBootAfterAttribute(Type type, string method = null)
        {
            Type = type;
            Method = method;
        }
    }
}
