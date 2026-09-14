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
            context.Station = null;
            context.IsLeaving = true;

            if (!context.Locomotion.MoveTo(context.ExitPoint))
                context.ReadyToDespawn = true;
        }

        public override void Tick(CustomerContext context, float deltaTime)
        {
            if (context.ReadyToDespawn) return;

            float distance = UnityEngine.Vector3.Distance(
                context.Agent.transform.position, context.ExitPoint);

            if (distance <= DespawnDistance)
            {
                Depart(context, true);
                return;
            }

            // Sem caminho ou preso: some assim que der, sem travar a loja.
            if (context.Locomotion.PathFailed || context.StateTime >= GiveUpAfterSeconds)
                Depart(context, false);
        }

        private static void Depart(CustomerContext context, bool reachedDoor)
        {
            context.ReadyToDespawn = true;

            // Só publica CustomerLeft aqui se o Frustrated não tiver publicado.
            if (context.PendingFrustration.HasValue) return;

            bool satisfied = !context.Basket.IsEmpty || context.SaleFinished;

            context.Events?.Publish(new CustomerLeft(
                context.Agent != null ? context.Agent.Id : 0,
                satisfied,
                context.StoreClosed ? CustomerLeaveReason.StoreClosed
                                    : CustomerLeaveReason.Purchased));
        }

    }
}
