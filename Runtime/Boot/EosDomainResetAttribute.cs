using System;

namespace EOS.Unity
{
    /// <summary>Marks a static parameterless void method to be invoked on every domain reset to clear static state; reflection-discovered by <see cref="EosDomainResetRunner"/>.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class EosDomainResetAttribute : Attribute
    {
    }
}
