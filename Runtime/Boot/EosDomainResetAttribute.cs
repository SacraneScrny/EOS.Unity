using System;

namespace EOS.Unity
{
    /// <summary>
    /// Marks a <c>static</c> parameterless method to run during the EOS domain-reset
    /// stage (<c>SubsystemRegistration</c>), right after the core reset. Unlike
    /// <see cref="EosBootAttribute"/> these are discovered by reflection at runtime — no
    /// codegen, no ordering guarantees. Any access modifier is allowed.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class EosDomainResetAttribute : Attribute
    {
    }
}
