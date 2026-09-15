using System;
using JapanMarket.Core;

namespace JapanMarket.Domain
{

    public interface IExpenseSource
    {

        string Label { get; }

        TransactionReason Reason { get; }

        Money GetDailyCost();
    }

    public readonly struct ExpenseLine
    {
        public readonly string Label;
        public readonly TransactionReason Reason;
        public readonly Money Amount;

        public ExpenseLine(string label, TransactionReason reason, Money amount)
        {
            Label = label;
            Reason = reason;
            Amount = amount;
        }

        public override string ToString() => $"{Label}: {Amount}";
    }

    public sealed class FlatExpense : IExpenseSource
    {
        private readonly Func<Money> _amount;

        public FlatExpense(string label, TransactionReason reason, Money amount)
            : this(label, reason, () => amount) { }

        public FlatExpense(string label, TransactionReason reason, Func<Money> amount)
        {
            Label = label;
            Reason = reason;
            _amount = amount ?? (() => Money.Zero);
        }

        public string Label { get; }
        public TransactionReason Reason { get; }

        public Money GetDailyCost() => _amount();
    }
}
