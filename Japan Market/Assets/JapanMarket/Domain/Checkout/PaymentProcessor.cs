using System.Collections.Generic;
using System.Collections.ObjectModel;
using JapanMarket.Core;

namespace JapanMarket.Domain
{

    public static class PaymentProcessor
    {

        public static readonly IReadOnlyList<Money> JapaneseDenominations =
            new ReadOnlyCollection<Money>(new[]
            {
                Money.FromYen(1),    Money.FromYen(5),    Money.FromYen(10),
                Money.FromYen(50),   Money.FromYen(100),  Money.FromYen(500),
                Money.FromYen(1000), Money.FromYen(2000), Money.FromYen(5000),
                Money.FromYen(10000),
            });

        public static Money CalculateChange(Money total, Money tendered) =>
            Money.Max(Money.Zero, tendered - total);

        public static bool IsChangeCorrect(Money total, Money tendered, Money given) =>
            given == CalculateChange(total, tendered);

        public static bool IsTypedAmountCorrect(Money total, Money typed) => typed == total;

        public static Money RollTenderedAmount(Money total, IReadOnlyList<Money> denominations)
        {
            if (total <= Money.Zero) return Money.Zero;
            if (denominations == null || denominations.Count == 0) return total;

            Money best = Money.Zero;
            bool found = false;

            for (int i = 0; i < denominations.Count; i++)
            {
                Money note = denominations[i];
                if (note < total) continue;
                if (found && note >= best) continue;

                best = note;
                found = true;
            }

            if (found) return best;

            Money largest = Money.Zero;
            for (int i = 0; i < denominations.Count; i++)
                largest = Money.Max(largest, denominations[i]);

            if (largest <= Money.Zero) return total;

            Money tendered = Money.Zero;
            while (tendered < total) tendered += largest;

            return tendered;
        }
    }
}
