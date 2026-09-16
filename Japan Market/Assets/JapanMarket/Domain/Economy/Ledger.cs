using System;
using System.Collections.Generic;
using JapanMarket.Core;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Implementação padrão do livro-razão. C# puro.
    /// </summary>
    public sealed class Ledger : ILedger, IDisposable
    {
        private readonly IEventBus _events;
        private readonly IGameClock _clock;
        private readonly List<Transaction> _today = new();

        private IDisposable _daySubscription;

        public Ledger(IEventBus events, IGameClock clock, Money openingBalance)
        {
            _events = events;
            _clock = clock;

            Balance = openingBalance;

            // O dia zera sozinho quando o relógio vira. Deixar isso a cargo de
            // quem vira o dia significaria um lugar a mais para esquecer.
            if (_events != null)
                _daySubscription = _events.Subscribe<DayStarted>(_ => _today.Clear());
        }

        public Money Balance { get; private set; }

        public IReadOnlyList<Transaction> Today => _today;

        public event Action<Transaction> Recorded;

        public bool CanAfford(Money amount) => amount <= Balance;

        public void Deposit(Money amount, TransactionReason reason, string note = null)
        {
            if (!amount.IsPositive) return;
            Record(amount, reason, note);
        }

        public bool TryWithdraw(Money amount, TransactionReason reason, string note = null)
        {
            if (!amount.IsPositive) return false;
            if (!CanAfford(amount)) return false;

            Record(-amount, reason, note);
            return true;
        }

        public void Charge(Money amount, TransactionReason reason, string note = null)
        {
            if (!amount.IsPositive) return;
            Record(-amount, reason, note);
        }

        private void Record(Money delta, TransactionReason reason, string note)
        {
            Money previous = Balance;
            Balance = previous + delta;

            var transaction = new Transaction(delta, reason, _clock?.Day ?? 0, note);
            _today.Add(transaction);

            // O saldo já está atualizado antes de qualquer aviso: quem reage
            // lendo Balance nunca vê o valor de antes.
            Recorded?.Invoke(transaction);

            if (_events == null) return;

            _events.Publish(new BalanceChanged(previous, Balance, reason));
            _events.Publish(new TransactionRecorded(delta, reason, transaction.Day));
        }

        /// <summary>Restaura um save sem gerar movimentação nem evento.</summary>
        public void Restore(Money balance, IReadOnlyList<Transaction> today = null)
        {
            Balance = balance;

            _today.Clear();
            if (today != null) _today.AddRange(today);
        }

        public void Dispose()
        {
            _daySubscription?.Dispose();
            _daySubscription = null;
        }
    }
}
