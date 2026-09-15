using JapanMarket.Core;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// Alguma coisa deu errado e o cliente vai embora sem comprar.
    ///
    /// Existe como estado próprio, em vez de "ir direto para a saída", por dois
    /// motivos. O jogador precisa VER que perdeu a venda e por quê — é o
    /// feedback que ensina a manter a loja limpa, com caixa e com preço
    /// razoável. E os produtos que ele já tinha pego precisam sair da cesta e
    /// voltar para o estoque, senão a loja perde mercadoria a cada desistência.
    /// </summary>
    public sealed class FrustratedState : CustomerStateBase
    {
        private const float ReactionSeconds = 1.6f;

        public override void Enter(CustomerContext context)
        {
            context.Locomotion.Halt();
            context.WaitUntil = ReactionSeconds;
            context.FrustrationDone = false;

            // Ele pode ter chegado aqui no meio de uma ida à prateleira. Soltar
            // o slot agora, e não só lá no LeavingState, mantém a invariante de
            // que ninguém segura reserva por mais tempo do que precisa.
            context.ClearShelfTarget();

            // Mesma razão para o caixa: se ele desistiu na fila ou no balcão,
            // é agora que sai de lá — senão o lugar dele continua ocupado
            // enquanto ele reclama e caminha até a porta.
            context.ReleaseStation();

            CustomerLeaveReason reason = context.PendingFrustration ?? CustomerLeaveReason.NothingToBuy;

            context.ReturnBasketToShelves();

            // A loja fica sabendo agora, não quando ele cruzar a porta: o
            // relatório do dia e os objetivos reagem ao motivo, e o jogador vê
            // o balão de reclamação enquanto o cliente ainda está na frente dele.
            context.Events?.Publish(new CustomerLeft(
                context.Agent != null ? context.Agent.Id : 0, false, reason));
        }

        public override void Tick(CustomerContext context, float deltaTime)
        {
            if (context.StateTime >= context.WaitUntil) context.FrustrationDone = true;
        }

        public static bool ReactionOver(CustomerContext context) => context.FrustrationDone;
    }
}
