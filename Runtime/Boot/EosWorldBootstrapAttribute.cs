using System;

namespace EOS.Unity
{
    /// <summary>
    /// Marks a <c>public static void Method(World world)</c> as a per-world bootstrap step. The
    /// editor collects every such method across the project, sorts them (<see cref="Order"/>, then a
    /// deterministic type/method name tie-break) and bakes the call list into a generated
    /// WorldBootstrap. The core invokes that list through <c>EOS.Loader.WorldBootstrap.Provider</c>
    /// on every <c>World.Init()</c> and <c>World.Reset()</c>, so the default world and any world
    /// created later all run through the same steps — register services, push context defaults, etc.
    /// </summary>
    /// <remarks>
    /// The method must be <c>static</c>, <c>public</c>, return <c>void</c> and take a single
    /// <see cref="EOS.Core.World"/> parameter on a public non-generic type; other shapes are skipped
    /// with a warning so the generated file always compiles. The handler never references the
    /// bootstrap itself — it just receives the world.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class EosWorldBootstrapAttribute : Attribute
    {
        /// <summary>Coarse ordering key; lower runs earlier. Ties broken deterministically by type/method name.</summary>
        public int Order { get; }

        public EosWorldBootstrapAttribute(int order = 0)
        {
            Order = order;
        }
    }
}
