using JapanMarket.Core;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Uma linha do livro-razão.
    ///
    /// O sinal fica no <see cref="Amount"/>: entrada positiva, saída negativa.
    /// Não existe um bool "isExpense" ao lado — dois campos que precisam
    /// concordar são dois campos que um dia discordam.
    /// </summary>
    public readonly struct Transaction
    {
        public readonly Money Amount;
        public readonly TransactionReason Reason;
        public readonly int Day;

        /// <summary>Detalhe opcional para a tela: "Ketchup × 8", "Prateleira".</summary>
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
