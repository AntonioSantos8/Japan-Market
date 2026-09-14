using JapanMarket.Core;
using JapanMarket.Domain;

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

            CustomerLeaveReason reason = context.PendingFrustration ?? CustomerLeaveReason.NothingToBuy;

            AbandonBasket(context);

            // A loja fica sabendo agora, não quando ele cruzar a porta: o
            // relatório do dia e os objetivos reagem ao motivo, e o jogador vê
            // o balão de reclamação enquanto o cliente ainda está na frente dele.
            context.Events?.Publish(new CustomerLeft(
                context.Agent != null ? context.Agent.Id : 0, false, reason));
        }

        /// <summary>
        /// Devolve o que estava na cesta. Se a prateleira de origem sumiu ou
        /// encheu, o produto simplesmente evapora — melhor perder a unidade do
        /// que deixar o cliente travado tentando devolver.
        /// </summary>
        private static void AbandonBasket(CustomerContext context)
        {
            if (context.Basket == null || context.Basket.IsEmpty) return;

            while (context.Basket.TryTakeFirst(out CustomerBasket.Entry entry))
                TryReturn(context, entry);

            context.Animation?.SetCarrying(false);
        }

        private static void TryReturn(CustomerContext context, CustomerBasket.Entry entry)
        {
            var shelves = context.Furniture.WithCapability<IProductStorage>();

            for (int i = 0; i < shelves.Count; i++)
            {
                IProductStorage shelf = shelves[i];

                if (shelf?.Owner == null || !shelf.Owner.IsAlive) continue;
                if (!shelf.Accepts(entry.Product)) continue;
                if (shelf.TryPlace(entry.Product, out _)) return;
            }
        }

        public override void Tick(CustomerContext context, float deltaTime)
        {
            if (context.StateTime >= context.WaitUntil) context.FrustrationDone = true;
        }

        public static bool ReactionOver(CustomerContext context) => context.FrustrationDone;
    }
}
