using System.Collections.Generic;
using JapanMarket.Core;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Quem cobra as contas do dia.
    ///
    /// Duas operações distintas de propósito: <see cref="Preview"/> apura sem
    /// cobrar (é o que a tela de fim de dia mostra) e <see cref="ChargeDay"/>
    /// cobra. Uma tela que precisasse cobrar para saber quanto mostrar seria uma
    /// tela que não pode ser aberta duas vezes.
    /// </summary>
    public interface IExpenseService
    {
        void Register(IExpenseSource source);
        void Unregister(IExpenseSource source);

        /// <summary>
        /// Apura sem cobrar. Só as fontes com valor acima de zero.
        ///
        /// A lista é reaproveitada entre chamadas — leia agora, não guarde a
        /// referência para o frame seguinte.
        /// </summary>
        IReadOnlyList<ExpenseLine> Preview();

        /// <summary>Soma do Preview.</summary>
        Money PreviewTotal();

        /// <summary>
        /// Cobra tudo, uma linha por fonte. Usa <c>Charge</c>, não
        /// <c>TryWithdraw</c>: conta de luz chega com ou sem saldo.
        /// </summary>
        Money ChargeDay();
    }

    public sealed class ExpenseService : IExpenseService
    {
        private readonly ILedger _ledger;
        private readonly List<IExpenseSource> _sources = new();
        private readonly List<ExpenseLine> _buffer = new();

        public ExpenseService(ILedger ledger) => _ledger = ledger;

        public IReadOnlyList<IExpenseSource> Sources => _sources;

        public void Register(IExpenseSource source)
        {
            if (source == null || _sources.Contains(source)) return;
            _sources.Add(source);
        }

        public void Unregister(IExpenseSource source)
        {
            if (source == null) return;
            _sources.Remove(source);
        }

        public IReadOnlyList<ExpenseLine> Preview()
        {
            _buffer.Clear();

            for (int i = 0; i < _sources.Count; i++)
            {
                IExpenseSource source = _sources[i];
                if (source == null) continue;

                Money cost = source.GetDailyCost();
                if (!cost.IsPositive) continue;

                _buffer.Add(new ExpenseLine(source.Label, source.Reason, cost));
            }

            return _buffer;
        }

        public Money PreviewTotal()
        {
            IReadOnlyList<ExpenseLine> lines = Preview();

            Money total = Money.Zero;
            for (int i = 0; i < lines.Count; i++) total += lines[i].Amount;

            return total;
        }

        public Money ChargeDay()
        {
            if (_ledger == null) return Money.Zero;

            // Cópia antes de cobrar: uma cobrança pode, por evento, fazer alguém
            // registrar ou remover uma fonte — e a lista seria mutada no meio da
            // iteração.
            Preview();
            ExpenseLine[] lines = _buffer.ToArray();

            Money total = Money.Zero;

            for (int i = 0; i < lines.Length; i++)
            {
                ExpenseLine line = lines[i];

                _ledger.Charge(line.Amount, line.Reason, line.Label);
                total += line.Amount;
            }

            return total;
        }
    }
}
