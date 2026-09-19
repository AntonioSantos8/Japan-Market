using System.Collections.Generic;
using JapanMarket.Core;

namespace JapanMarket.Domain
{

    public sealed class DailyReport
    {
        private readonly Dictionary<CustomerLeaveReason, int> _lostByReason = new();
        private readonly List<ExpenseLine> _expenses = new();

        public DailyReport(int day, Money openingBalance)
        {
            Day = day;
            OpeningBalance = openingBalance;
            ClosingBalance = openingBalance;
        }

        public int Day { get; }

        public Money OpeningBalance { get; }
        public Money ClosingBalance { get; internal set; }

        public Money Revenue { get; internal set; }

        public Money CostOfGoods { get; internal set; }

        public Money Purchases { get; internal set; }

        public Money Expenses { get; internal set; }

        public IReadOnlyList<ExpenseLine> ExpenseLines => _expenses;

        public int CustomersServed { get; internal set; }
        public int CustomersLost { get; internal set; }
        public int ItemsSold { get; internal set; }

        public IReadOnlyDictionary<CustomerLeaveReason, int> LostByReason => _lostByReason;

        public Money GrossProfit => Revenue - CostOfGoods;

        public Money NetProfit => GrossProfit - Expenses;

        public Money CashFlow => ClosingBalance - OpeningBalance;

        public Money AverageTicket =>
            CustomersServed > 0 ? Money.FromYen(Revenue.Yen / (double)CustomersServed) : Money.Zero;

        public bool IsClosed { get; internal set; }

        internal void CountLost(CustomerLeaveReason reason)
        {
            CustomersLost++;
            _lostByReason.TryGetValue(reason, out int count);
            _lostByReason[reason] = count + 1;
        }

        internal void SetExpenses(IReadOnlyList<ExpenseLine> lines, Money total)
        {
            _expenses.Clear();
            if (lines != null) _expenses.AddRange(lines);

            Expenses = total;
        }

        public override string ToString() =>
            $"Day {Day}: revenue {Revenue}, cost {CostOfGoods}, expenses {Expenses}, " +
            $"profit {NetProfit} · {CustomersServed} served, {CustomersLost} lost";
    }
}
