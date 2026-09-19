namespace JapanMarket.Data
{
    /// <summary>
    /// O lado de ESCRITA das flags de progresso.
    ///
    /// <see cref="IUnlockContext"/> só lê, e isso é de propósito: uma condição de
    /// desbloqueio que pudesse levantar flags seria uma condição capaz de se
    /// satisfazer sozinha. Quem concede progresso — hoje o serviço de objetivos,
    /// amanhã o tutorial — pede esta interface, e só ela.
    /// </summary>
    public interface IProgressFlags
    {
        bool HasFlag(string flag);

        /// <summary>Idempotente: levantar duas vezes é levantar uma.</summary>
        void RaiseFlag(string flag);
    }
}
