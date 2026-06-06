using System;

namespace EOS.Unity
{
    [Serializable]
    public abstract class ComponentSetModule
    {
        public abstract void Build(ComponentSetBuilder builder);
    }
}
