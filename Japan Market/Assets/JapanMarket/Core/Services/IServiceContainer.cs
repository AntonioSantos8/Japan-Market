namespace JapanMarket.Core
{

    public interface IServiceContainer
    {
        void Register<T>(T service) where T : class;
        void Unregister<T>() where T : class;

        T Resolve<T>() where T : class;

        bool TryResolve<T>(out T service) where T : class;

        bool IsRegistered<T>() where T : class;
        void Clear();
    }
}
