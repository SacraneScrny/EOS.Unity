using UnityEngine;

namespace EOS.Unity
{
    /// <summary>Authoring entry pairing a socket id on the blueprint's incarnation with a nested <see cref="EntityBlueprint"/> that is built into the same world and attached there; the inspector picks the socket from the selected incarnation's <see cref="SocketSet"/>.</summary>
    [System.Serializable]
    public sealed class BlueprintModule
    {
        /// <summary>The socket on the entity's incarnation to attach the built module into.</summary>
        public string SocketId;
        /// <summary>The nested blueprint built into the same world and attached as the socket's module.</summary>
        [SerializeReference]
        [SubclassSelector]
        public EntityBlueprint Module;
    }
}
