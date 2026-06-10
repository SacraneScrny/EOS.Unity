namespace EOS.Unity
{
    public interface IPoolableView
    {
        void OnRent();
        void OnReturn();
    }
}
