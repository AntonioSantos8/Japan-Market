using System;
using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;

namespace JapanMarket.Domain
{
    public sealed class BankService : IBankService, IDisposable
    {
        private readonly ILedger _ledger;
        private readonly IExpenseService _expenses;
        private readonly IEventBus _events;
        private readonly IUnlockContext _unlockContext;

        private readonly List<ActiveLoan> _activeLoans = new();
        private readonly Dictionary<ActiveLoan, IExpenseSource> _loanSources = new();

        private IDisposable _dayEndedSub;

        public IReadOnlyList<ActiveLoan> ActiveLoans => _activeLoans;

        public event Action<ActiveLoan> LoanTaken;
        public event Action<ActiveLoan> LoanPaidOff;

        public BankService(ILedger ledger, IExpenseService expenses, IUnlockContext unlockContext, IEventBus events = null)
        {
            _ledger = ledger;
            _expenses = expenses;
            _unlockContext = unlockContext;
            _events = events;

            if (_events != null)
            {
                _dayEndedSub = _events.Subscribe<DayEnded>(OnDayEnded);
            }
        }

        public bool TryTakeLoan(LoanDefinition loan)
        {
            if (loan == null || _ledger == null) return false;
            if (!loan.IsUnlocked(_unlockContext)) return false;

            for (int i = 0; i < _activeLoans.Count; i++)
            {
                if (_activeLoans[i].Definition == loan) return false;
            }

            var activeLoan = new ActiveLoan(loan);
            _activeLoans.Add(activeLoan);

            _ledger.Deposit(loan.Principal, TransactionReason.LoanReceived, $"Loan: {loan.DisplayName.Value}");
            var source = new FlatExpense($"Loan {loan.DisplayName.Value}", TransactionReason.LoanInstallment, loan.DailyPayment);
            _loanSources[activeLoan] = source;
            _expenses?.Register(source);

            LoanTaken?.Invoke(activeLoan);
            return true;
        }

        public bool TryPayOffEarly(ActiveLoan loan)
        {
            if (loan == null || !_activeLoans.Contains(loan) || _ledger == null) return false;

            Money balance = loan.BalanceToPayOff;

            if (!_ledger.TryWithdraw(balance, TransactionReason.LoanInstallment, $"Early Payoff: {loan.Definition.DisplayName.Value}"))
            {
                return false;
            }

            FinishLoan(loan);
            return true;
        }

        private void OnDayEnded(DayEnded e)
        {
            ActiveLoan[] loans = _activeLoans.ToArray();

            foreach (var loan in loans)
            {
                loan.RecordPayment();
                if (loan.IsPaidOff)
                {
                    FinishLoan(loan);
                }
            }
        }

        private void FinishLoan(ActiveLoan loan)
        {
            _activeLoans.Remove(loan);

            if (_loanSources.TryGetValue(loan, out var source))
            {
                _expenses?.Unregister(source);
                _loanSources.Remove(loan);
            }

            LoanPaidOff?.Invoke(loan);
        }

        public void Restore(IEnumerable<ActiveLoan> loans)
        {
            _activeLoans.Clear();
            foreach (var kvp in _loanSources)
            {
                _expenses?.Unregister(kvp.Value);
            }
            _loanSources.Clear();

            if (loans != null)
            {
                foreach (var loan in loans)
                {
                    _activeLoans.Add(loan);
                    var source = new FlatExpense($"Loan {loan.Definition.DisplayName.Value}", TransactionReason.LoanInstallment, loan.DailyPayment);
                    _loanSources[loan] = source;
                    _expenses?.Register(source);
                }
            }
        }

        public void Dispose()
        {
            _dayEndedSub?.Dispose();
            _dayEndedSub = null;
        }
    }
}

