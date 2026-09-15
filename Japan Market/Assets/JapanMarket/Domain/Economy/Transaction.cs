using JapanMarket.Core;

namespace JapanMarket.Domain
{

    public readonly struct Transaction
    {
        public readonly Money Amount;
        public readonly TransactionReason Reason;
        public readonly int Day;

        public readonly string Note;

        public Transaction(Money amount, TransactionReason reason, int day, string note = null)
        {
            Amount = amount;
            Reason = reason;
            Day = day;
            Note = note;
        }

        public bool IsIncome => Amount.IsPositive;
        public bool IsExpense => Amount.IsNegative;

        public override string ToString() =>
            string.IsNullOrEmpty(Note)
                ? $"[D{Day}] {Reason}: {Amount}"
                : $"[D{Day}] {Reason}: {Amount} ({Note})";
    }
}
