namespace JapanMarket.Gameplay
{
    /// <summary>
    /// Entrou pela porta. Caminha até o ponto de entrada e faz uma pausa curta
    /// antes de começar a comprar — sem ela, todos os clientes saem andando no
    /// mesmo instante e a loja vira um formigueiro.
    /// </summary>
    public sealed class EnteringState : CustomerStateBase
    {
        public override void Enter(CustomerContext context)
        {
            context.WaitUntil = context.Profile != null ? context.Profile.RollEntryDelay() : 1f;
            context.ShelvesRemaining = context.Profile != null
                ? context.Profile.RollShelvesToVisit()
                : 1;

            context.Locomotion.MoveTo(context.EntryPoint);
        }

        public override void Tick(CustomerContext context, float deltaTime)
        {
            // Chegou, ou não vai chegar. Os dois casos param aqui: sem o teste
            // de PathFailed, um cliente com a porta bloqueada ficava preso neste
            // estado indefinidamente, porque Finished exige IsHalted e só o
            // HasArrived levava a Halt().
            if (context.Locomotion.IsHalted) return;

            if (context.Locomotion.HasArrived || context.Locomotion.PathFailed)
                context.Locomotion.Halt();
        }

        /// <summary>Condição da tabela de transições.</summary>
        public static bool Finished(CustomerContext context) =>
            context.Locomotion.IsHalted && context.StateTime >= context.WaitUntil;
    }
}
