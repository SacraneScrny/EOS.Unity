namespace EOS.Unity
{
    /// <summary>Implement on view components to receive pool rent/return notifications and reset instance state, since the pool does not.</summary>
    public interface IPoolableView
    {
        /// <summary>Called when the instance is rented from the pool and reactivated; restore state for reuse here.</summary>
        void OnRent();
        /// <summary>Called when the instance is returned to the pool and deactivated; clear transient state here.</summary>
        void OnReturn();
    }
}
