using System;
using System.Collections.Generic;
using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Implementação padrão do <see cref="ICheckoutService"/>. C# puro: recebe
    /// o registro de móveis e o barramento, e não toca em nada da cena.
    /// </summary>
    public sealed class CheckoutService : ICheckoutService
    {
        private readonly IFurnitureRegistry _furniture;
        private readonly IEventBus _events;

        public CheckoutService(IFurnitureRegistry furniture, IEventBus events)
        {
            _furniture = furniture ?? throw new ArgumentNullException(nameof(furniture));
            _events = events;   // opcional: sem barramento, a venda fecha em silêncio
        }

        public event Action<CheckoutSession> SaleCompleted;

        // ── escolha da estação ───────────────────────────────────────────────

        /// <summary>
        /// Política: entre as operantes e alcançáveis, a de menor fila; empate
        /// desempatado pela mais perto.
        ///
        /// O desempate por distância não é enfeite. Sem ele, com duas caixas
        /// vazias, TODO cliente escolhe a mesma — a primeira do registro — e a
        /// segunda caixa que o jogador comprou nunca é usada até a primeira
        /// encher.
        /// </summary>
        public bool TryFindBestStation(Vector3 from, Predicate<Vector3> canReach,
                                       out ICheckoutStation station)
        {
            station = null;

            IReadOnlyList<ICheckoutStation> candidates =
                _furniture.WithCapability<ICheckoutStation>();

            int bestQueue = int.MaxValue;
            float bestDistanceSqr = float.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                ICheckoutStation candidate = candidates[i];
                if (!IsUsable(candidate)) continue;

                Vector3 entry = candidate.GetQueuePosition(candidate.QueueLength);
                if (canReach != null && !canReach(entry)) continue;

                int queue = candidate.QueueLength;
                if (queue > bestQueue) continue;

                float distanceSqr = (entry - from).sqrMagnitude;
                if (queue == bestQueue && distanceSqr >= bestDistanceSqr) continue;

                station = candidate;
                bestQueue = queue;
                bestDistanceSqr = distanceSqr;
            }

            return station != null;
        }

        /// <summary>
        /// Viva, operante e com dono válido.
        ///
        /// A checagem de <c>Owner.IsAlive</c> tem que ficar aqui e não confiar
        /// só no registro: entre o móvel ser destruído e o <c>Unregister</c>
        /// rodar existe uma janela de um frame, e é exatamente nela que o
        /// cliente pediria um caixa e receberia um objeto morto.
        /// </summary>
        private static bool IsUsable(ICheckoutStation station) =>
            station != null
            && station.Owner != null
            && station.Owner.IsAlive
            && station.AcceptsNewCustomers;

        // ── fechamento da venda ──────────────────────────────────────────────

        public bool TryCompleteSale(ICheckoutStation station)
        {
            if (station == null) return false;

            CheckoutSession session = station.CurrentSession;
            if (session == null || session.IsComplete) return false;

            // Falta passar item: não é erro, é o jogador apertando cedo demais.
            if (!session.AllScanned) return false;

            if (session.Method == PaymentMethod.Cash && session.AmountTendered < session.Total)
                return false;

            session.MarkComplete();

            // A estação primeiro: ela tira o cliente da fila e avisa
            // SaleFinished. Só depois o mundo sabe da venda — assim ninguém que
            // reage ao evento encontra a loja num estado intermediário, com o
            // cliente já pago mas ainda ocupando o balcão.
            station.CloseSession(session, SessionCloseReason.Completed);

            Publish(station, session);
            SaleCompleted?.Invoke(session);

            return true;
        }

        private void Publish(ICheckoutStation station, CheckoutSession session)
        {
            if (_events == null) return;

            FurnitureId stationId = station.Owner != null ? station.Owner.Id : default;
            int customerId = session.Customer != null ? session.Customer.Id : 0;

            Money cost = Money.Zero;
            IReadOnlyList<SaleLine> lines = session.Lines;
            for (int i = 0; i < lines.Count; i++) cost += lines[i].Cost;

            _events.Publish(new SaleCompleted(
                customerId, stationId, session.Total, cost,
                lines.Count, session.Method));
        }
    }
}
