using JapanMarket.Domain;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// Base dos estados do cliente.
    ///
    /// Sobre onde guardar dados: cada cliente monta a própria máquina com as
    /// próprias instâncias de estado, então um campo privado aqui é seguro — é
    /// rascunho de um cliente só. A regra é outra: tudo que uma TRANSIÇÃO
    /// precisa ler vai para o <see cref="CustomerContext"/>, porque a tabela de
    /// transições só enxerga o contexto. Rascunho fica no estado; verdade
    /// compartilhada fica no contexto.
    /// </summary>
    public abstract class CustomerStateBase : IState<CustomerContext>
    {
        public virtual void Enter(CustomerContext context) { }
        public virtual void Tick(CustomerContext context, float deltaTime) { }
        public virtual void Exit(CustomerContext context) { }

        /// <summary>Para parada + encarar alvo, que quase todo estado faz ao chegar.</summary>
        protected static void HaltFacing(CustomerContext context, UnityEngine.Vector3 lookAt)
        {
            if (!context.Locomotion.IsHalted) context.Locomotion.Halt();
            context.Locomotion.FacePoint(lookAt);
        }
    }
}
