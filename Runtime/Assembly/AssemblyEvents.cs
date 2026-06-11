using EOS.Entities;

namespace EOS.Unity
{
    public readonly struct ModuleAttached
    {
        public readonly EosEntity Parent;
        public readonly EosEntity Module;
        public readonly string SocketId;

        public ModuleAttached(EosEntity parent, EosEntity module, string socketId)
        {
            Parent = parent;
            Module = module;
            SocketId = socketId;
        }
    }

    public readonly struct ModuleDetached
    {
        public readonly EosEntity Parent;
        public readonly EosEntity Module;
        public readonly string SocketId;

        public ModuleDetached(EosEntity parent, EosEntity module, string socketId)
        {
            Parent = parent;
            Module = module;
            SocketId = socketId;
        }
    }
}
