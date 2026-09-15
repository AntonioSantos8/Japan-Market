using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Domain;

namespace JapanMarket.Gameplay
{

    public sealed class AtCounterState : CustomerStateBase
    {

        private const float OpenTimeoutSeconds = 3f;

        private readonly List<SaleLine> _lines = new();

        public override void Enter(CustomerContext context)
        {
            context.SaleFinished = false;
            context.Session = null;

            if (context.StationLost) return;

            HaltFacing(context, context.Station.CounterPosition);
            TryOpen(context);
        }

        public override void Tick(CustomerContext context, float deltaTime)
        {
            if (context.StationLost) return;

            if (context.Session == null)
            {

                if (context.StateTime < OpenTimeoutSeconds) { TryOpen(context); return; }

                context.CheckoutLost = true;
                return;
            }

            if (!ReferenceEquals(context.Session, context.Station.CurrentSession)
                && !context.Session.IsComplete)
            {
                context.Session = null;
                context.CheckoutLost = true;
                return;
            }

            if (QueueingState.OutOfPatience(context))
                context.Frustrate(CustomerLeaveReason.WaitedTooLong);
        }

        public override void Exit(CustomerContext context)
        {
            _lines.Clear();

            if (!context.SaleFinished) return;

            context.Basket.Clear();

            if (context.Animation != null) context.Animation.SetCarrying(false);
        }

        private void TryOpen(CustomerContext context)
        {
            if (!context.Station.IsFront(context.Agent)) return;

            BuildLines(context);
            if (_lines.Count == 0)
            {

                context.Frustrate(CustomerLeaveReason.NothingToBuy);
                return;
            }

            PaymentMethod method = context.Profile != null && context.Profile.RollPrefersCard()
                ? PaymentMethod.Card
                : PaymentMethod.Cash;

            if (!context.Station.TryOpenSession(context.Agent, _lines, method,
                                                out CheckoutSession session))
                return;

            if (method == PaymentMethod.Cash)
            {
                session.SetAmountTendered(PaymentProcessor.RollTenderedAmount(
                    session.Total, PaymentProcessor.JapaneseDenominations));
            }

            context.Session = session;
        }

        private void BuildLines(CustomerContext context)
        {
            _lines.Clear();

            IReadOnlyList<CustomerBasket.Entry> entries = context.Basket.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                CustomerBasket.Entry entry = entries[i];
                if (entry.Product == null) continue;   

                _lines.Add(new SaleLine(entry.Product, entry.PricePaid));
            }
        }

        public static bool Paid(CustomerContext context) => context.SaleFinished;

        public static bool LostStation(CustomerContext context) =>
            context.StationLost || context.CheckoutLost;
    }
}
