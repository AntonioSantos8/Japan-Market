using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Domain;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// Procura um caixa para pagar.
    ///
    /// O cliente nunca guarda referência a um caixa específico: ele pergunta ao
    /// registro quem tem a capacidade de checkout, escolhe a melhor no momento,
    /// e volta para cá se aquela sumir. É essa indireção que faz "remover a
    /// caixa registradora durante o expediente" ser um caso normal em vez de
    /// uma cascata de MissingReferenceException.
    ///
    /// Enquanto a Fase 5 não trouxer a implementação de ICheckoutStation, este
    /// estado não encontra nada e o cliente sai reclamando — que é exatamente o
    /// comportamento correto de uma loja sem caixa.
    /// </summary>
    public sealed class SeekingCheckoutState : CustomerStateBase
    {
        public override void Enter(CustomerContext context)
        {
            context.Station = null;
            context.CheckoutLost = false;
            context.Locomotion.Halt();

            TryFindStation(context);
        }

        public override void Tick(CustomerContext context, float deltaTime)
        {
            // Um caixa pode ser ligado ou colocado enquanto ele espera. Só
            // desiste depois de um tempo, não no primeiro frame.
            if (context.Station == null) TryFindStation(context);
        }

        private void TryFindStation(CustomerContext context)
        {
            IReadOnlyList<ICheckoutStation> stations =
                context.Furniture.WithCapability<ICheckoutStation>();

            ICheckoutStation best = null;
            int bestQueue = int.MaxValue;

            for (int i = 0; i < stations.Count; i++)
            {
                ICheckoutStation station = stations[i];

                if (station?.Owner == null || !station.Owner.IsAlive) continue;
                if (!station.IsOperational) continue;
                if (station.QueueLength >= bestQueue) continue;
                if (!context.Locomotion.CanReach(station.QueueAnchor)) continue;

                best = station;
                bestQueue = station.QueueLength;
            }

            if (best == null)
            {
                // Só reclama depois de esperar um pouco — evita o cliente
                // desistir no frame em que o jogador está reposicionando o caixa.
                if (context.StateTime >= GiveUpAfterSeconds)
                    context.Frustrate(CustomerLeaveReason.NoCheckout);
                return;
            }

            context.Station = best;
            context.QueueIndex = best.QueueLength;
        }

        private const float GiveUpAfterSeconds = 5f;

        public static bool FoundStation(CustomerContext context) => !context.StationLost;
    }
}
