namespace JapanMarket.Core
{
    /// <summary>
    /// Registro de serviços do jogo, com ciclo de vida explícito.
    ///
    /// Diferenças em relação ao ServiceLocator estático que ele substitui:
    ///  • <see cref="Resolve{T}"/> lança em vez de devolver null;
    ///  • existe <see cref="Unregister{T}"/> e <see cref="Clear"/>, então trocar
    ///    de cena não deixa referências mortas apontando para objetos destruídos;
    ///  • os serviços são registrados por INTERFACE, não por classe concreta, o
    ///    que permite substituir por um fake em teste.
    /// </summary>
    public interface IServiceContainer
    {
        void Register<T>(T service) where T : class;
        void Unregister<T>() where T : class;

        /// <summary>Lança <see cref="ServiceNotRegisteredException"/> se ausente.</summary>
        T Resolve<T>() where T : class;

        /// <summary>Para quando a ausência é um estado válido do jogo.</summary>
        bool TryResolve<T>(out T service) where T : class;

        bool IsRegistered<T>() where T : class;
        void Clear();
    }
}
