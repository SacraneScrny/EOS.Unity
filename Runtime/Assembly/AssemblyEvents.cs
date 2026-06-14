using EOS.Entities;

namespace EOS.Unity
{
    /// <summary>EOS event emitted when a module is attached to a parent socket; read it with an <c>EventExecute(ModuleAttached)</c> system method.</summary>
    public readonly struct ModuleAttached
    {
        /// <summary>The parent assembly entity the module was attached to.</summary>
        public readonly EosEntity Parent;
        /// <summary>The module entity that was attached.</summary>
        public readonly EosEntity Module;
        /// <summary>The socket the module now occupies.</summary>
        public readonly string SocketId;

        /// <summary>Creates the event for a parent, module, and socket.</summary>
        public ModuleAttached(EosEntity parent, EosEntity module, string socketId)
        {
            Parent = parent;
            Module = module;
            SocketId = socketId;
        }
    }

    /// <summary>EOS event emitted when a module is detached from a parent socket; read it with an <c>EventExecute(ModuleDetached)</c> system method.</summary>
    public readonly struct ModuleDetached
    {
        /// <summary>The parent assembly entity the module was detached from.</summary>
        public readonly EosEntity Parent;
        /// <summary>The module entity that was detached.</summary>
        public readonly EosEntity Module;
        /// <summary>The socket the module was released from.</summary>
        public readonly string SocketId;

        /// <summary>Creates the event for a parent, module, and socket.</summary>
        public ModuleDetached(EosEntity parent, EosEntity module, string socketId)
        {
            Parent = parent;
            Module = module;
            SocketId = socketId;
        }
    }
}
