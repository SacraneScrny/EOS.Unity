using EOS.Entities;
using UnityEngine;

namespace EOS.Unity
{
    public abstract class EntityIncarnation : MonoBehaviour
    {
        public EosEntity Entity { get; internal set; }

        internal void InvokeBind() => OnBind();
        internal void InvokeUnbind() => OnUnbind();
        internal void InvokeSync() => OnSync();
        internal void InvokeSyncFixed() => OnSyncFixed();
        internal void InvokeSyncLate() => OnSyncLate();

        protected virtual void OnBind() { }
        protected virtual void OnUnbind() { }
        protected virtual void OnSync() { }
        protected virtual void OnSyncFixed() { }
        protected virtual void OnSyncLate() { }
    }
}
