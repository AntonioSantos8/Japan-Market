using System;
using JapanMarket.Core;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Algo que cobra da loja no fim do dia.
    ///
    /// É a extensão que o item 15 pede: acrescentar "licença de bebida",
    /// "salário de repositor" ou "parcela do empréstimo" à conta do dia é
    /// registrar mais uma fonte, sem tocar em quem cobra e sem um switch sobre
    /// tipos de despesa.
    /// </summary>
    public interface IExpenseSource
    {
        /// <summary>Como aparece no relatório: "Aluguel", "Eletricidade".</summary>
        string Label { get; }

        TransactionReason Reason { get; }

        /// <summary>Quanto cobrar hoje. Zero significa "nada a cobrar".</summary>
        Money GetDailyCost();
    }

    /// <summary>Uma despesa já apurada, pronta para mostrar ou cobrar.</summary>
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

    /// <summary>
    /// Despesa de valor fixo — aluguel, taxa de licença. A mais comum, e não
    /// merece uma classe por caso.
    /// </summary>
    public sealed class FlatExpense : IExpenseSource
    {
        private readonly Func<Money> _amount;

        public FlatExpense(string label, TransactionReason reason, Money amount)
            : this(label, reason, () => amount) { }

        /// <summary>
        /// Versão com função para o valor que muda com o progresso — o aluguel
        /// sobe a cada expansão, e ninguém quer reregistrar a fonte por isso.
        /// </summary>
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
