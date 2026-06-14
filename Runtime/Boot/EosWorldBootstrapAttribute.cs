using System;

namespace EOS.Unity
{
    /// <summary>Marks a <c>public static void(World)</c> method as per-world seeding, run on every <c>World.Init()</c> and <c>World.Reset()</c>; collected into the generated world bootstrap.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class EosWorldBootstrapAttribute : Attribute
    {
        /// <summary>Sort priority among world-bootstrap methods; lower runs first.</summary>
        public int Order { get; }

        /// <summary>Marks the method as world bootstrap with the given <paramref name="order"/>.</summary>
        public EosWorldBootstrapAttribute(int order = 0)
        {
            Order = order;
        }
    }
}
