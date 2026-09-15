using JapanMarket.Core;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// Vai até a saída e desaparece.
    ///
    /// O despawn é decidido por distância, e não pela chegada do NavMeshAgent,
    /// porque vários clientes saindo ao mesmo tempo se atrapalham na porta e
    /// nenhum "chega" — é o bug de aglomeração que o código atual contorna com
    /// um destroy por distância. A diferença aqui é que existe também um plano
    /// B: se a saída for inalcançável, ele some em vez de ficar rodando na loja
    /// para sempre.
    /// </summary>
    public sealed class LeavingState : CustomerStateBase
    {
        private const float DespawnDistance = 1.5f;
        private const float GiveUpAfterSeconds = 30f;

        public override void Enter(CustomerContext context)
        {
            // Solta tudo o que ainda estivesse reservado. Se ele foi parar aqui
            // por uma transição global, no meio de qualquer coisa, é este Exit
            // coletivo que impede o vazamento.
            context.ClearShelfTarget();
            context.ReleaseStation();
            context.IsLeaving = true;

            // Sai com a cesta cheia sem ter pago — o caminho normal quando a
            // loja fecha com gente dentro. Sem devolver, a mercadoria já saiu da
            // prateleira e evapora junto com o cliente: o jogador perde estoque
            // toda vez que fecha a loja, em silêncio.
            if (!context.SaleFinished) context.ReturnBasketToShelves();

            if (context.Locomotion.MoveTo(context.ExitPoint)) return;

            // Nem caminho até a porta existe. Ainda assim a loja precisa saber
            // que ele foi embora: sair pelo atalho sem publicar CustomerLeft
            // faria a venda sumir do relatório do dia.
            Depart(context);
        }

        public override void Tick(CustomerContext context, float deltaTime)
        {
            if (context.ReadyToDespawn) return;

            float distance = UnityEngine.Vector3.Distance(
                context.Agent.transform.position, context.ExitPoint);

            if (distance <= DespawnDistance)
            {
                Depart(context);
                return;
            }

            // Sem caminho ou preso: some assim que der, sem travar a loja.
            if (context.Locomotion.PathFailed || context.StateTime >= GiveUpAfterSeconds)
                Depart(context);
        }

        private static void Depart(CustomerContext context)
        {
            if (context.ReadyToDespawn) return;   // já anunciado
            context.ReadyToDespawn = true;

            // Só publica CustomerLeft aqui se o Frustrated não tiver publicado.
            if (context.PendingFrustration.HasValue) return;

            // Satisfeito é quem PAGOU. A cesta não serve de critério: quem pagou
            // sai com ela vazia (o AtCounter a esvazia), e quem sai com ela
            // cheia é exatamente quem foi expulso pelo fechamento da loja sem
            // passar pelo caixa.
            CustomerLeaveReason reason =
                context.SaleFinished ? CustomerLeaveReason.Purchased
                : context.StoreClosed ? CustomerLeaveReason.StoreClosed
                : CustomerLeaveReason.NothingToBuy;

            context.Events?.Publish(new CustomerLeft(
                context.Agent != null ? context.Agent.Id : 0,
                context.SaleFinished,
                reason));
        }
    }
}
