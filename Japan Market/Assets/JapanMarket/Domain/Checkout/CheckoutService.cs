using System;
using System.Collections.Generic;
using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Domain
{

    public sealed class CheckoutService : ICheckoutService
    {
        private readonly IFurnitureRegistry _furniture;
        private readonly IEventBus _events;

        public CheckoutService(IFurnitureRegistry furniture, IEventBus events)
        {
            _furniture = furniture ?? throw new ArgumentNullException(nameof(furniture));
            _events = events;   
        }

        public event Action<CheckoutSession> SaleCompleted;

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

        private static bool IsUsable(ICheckoutStation station) =>
            station != null
            && station.Owner != null
            && station.Owner.IsAlive
            && station.AcceptsNewCustomers;

        public bool TryCompleteSale(ICheckoutStation station)
        {
            if (station == null) return false;

            CheckoutSession session = station.CurrentSession;
            if (session == null || session.IsComplete) return false;

            if (!session.AllScanned) return false;

            if (session.Method == PaymentMethod.Cash && session.AmountTendered < session.Total)
                return false;

            session.MarkComplete();

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
