using System;
using System.Collections.Generic;
using JapanMarket.Core;

namespace JapanMarket.Domain
{

    public interface IDailyReportService
    {

        DailyReport Current { get; }

        DailyReport GetReport(int day);

        IReadOnlyList<DailyReport> ClosedReports { get; }

        event Action<DailyReport> ReportClosed;
    }

    public sealed class DailyReportService : IDailyReportService, IDisposable
    {
        private readonly IEventBus _events;
        private readonly ILedger _ledger;
        private readonly List<DailyReport> _closed = new();
        private readonly List<IDisposable> _subscriptions = new();

        private bool _chargingExpenses;

        public DailyReportService(IEventBus events, ILedger ledger, int startingDay = 1)
        {
            _events = events;
            _ledger = ledger;

            Current = new DailyReport(startingDay, ledger?.Balance ?? Money.Zero);

            if (_events == null) return;

            _subscriptions.Add(_events.Subscribe<SaleCompleted>(OnSale));
            _subscriptions.Add(_events.Subscribe<CustomerLeft>(OnCustomerLeft));
            _subscriptions.Add(_events.Subscribe<TransactionRecorded>(OnTransaction));
        }

        public DailyReport Current { get; private set; }

        public IReadOnlyList<DailyReport> ClosedReports => _closed;

        public event Action<DailyReport> ReportClosed;

        public DailyReport GetReport(int day)
        {
            if (Current != null && Current.Day == day) return Current;

            for (int i = _closed.Count - 1; i >= 0; i--)
                if (_closed[i].Day == day) return _closed[i];

            return null;
        }

        private void OnSale(SaleCompleted sale)
        {
            Current.Revenue += sale.Revenue;
            Current.CostOfGoods += sale.Cost;
            Current.ItemsSold += sale.ItemCount;
            Current.CustomersServed++;
        }

        private void OnCustomerLeft(CustomerLeft left)
        {

            if (left.WasSatisfied || left.Reason == CustomerLeaveReason.Purchased) return;

            Current.CountLost(left.Reason);
        }

        private void OnTransaction(TransactionRecorded transaction)
        {
            if (_chargingExpenses) return;
            if (!transaction.Amount.IsNegative) return;

            Current.Purchases += -transaction.Amount;
        }

        public void BeginExpenseCharge() => _chargingExpenses = true;

        public void EndExpenseCharge() => _chargingExpenses = false;

        public DailyReport Close(int nextDay, IReadOnlyList<ExpenseLine> expenses, Money expenseTotal)
        {
            DailyReport closing = Current;

            closing.SetExpenses(expenses, expenseTotal);
            closing.ClosingBalance = _ledger?.Balance ?? closing.OpeningBalance;
            closing.IsClosed = true;

            _closed.Add(closing);

            Current = new DailyReport(nextDay, closing.ClosingBalance);

            ReportClosed?.Invoke(closing);
            return closing;
        }

        public void Dispose()
        {
            for (int i = 0; i < _subscriptions.Count; i++) _subscriptions[i]?.Dispose();
            _subscriptions.Clear();
        }
    }
}
