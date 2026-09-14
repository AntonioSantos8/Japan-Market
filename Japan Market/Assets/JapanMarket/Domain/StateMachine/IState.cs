namespace JapanMarket.Domain
{
    /// <summary>
    /// Um estado de uma <see cref="StateMachine{TContext}"/>.
    ///
    /// O contexto é passado em cada chamada em vez de ser guardado no estado.
    /// Isso é de propósito: estados ficam sem campo mutável próprio, então não
    /// existe "estado do estado" para dessincronizar — a única memória é o
    /// contexto, e ele é um lugar só.
    /// </summary>
    public interface IState<in TContext>
    {
        void Enter(TContext context);
        void Tick(TContext context, float deltaTime);

        /// <summary>
        /// Sempre chamado ao sair, por qualquer caminho — transição normal,
        /// transição global ou desligamento da máquina.
        ///
        /// É aqui que se libera o que o estado reservou. Enquanto a liberação
        /// dependia de o código de saída "lembrar" de chamá-la, o vazamento de
        /// slots do FurnitureOccupancy era inevitável.
        /// </summary>
        void Exit(TContext context);
    }
}
