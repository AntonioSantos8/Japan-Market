using System.Collections.Generic;
using JapanMarket.Core;

namespace JapanMarket.Domain
{

    public interface IExpenseService
    {
        void Register(IExpenseSource source);
        void Unregister(IExpenseSource source);

        IReadOnlyList<ExpenseLine> Preview();

        Money PreviewTotal();

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
