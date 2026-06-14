using System;

namespace EOS.Unity
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class EosDomainResetAttribute : Attribute
    {
    }
}
