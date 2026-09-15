using System;
using System.Collections.Generic;
using JapanMarket.Core;

namespace JapanMarket.Domain
{

    public sealed class DayCycle : IDisposable
    {
        private readonly IGameClock _clock;
        private readonly IExpenseService _expenses;
        private readonly DailyReportService _reports;
        private readonly IEventBus _events;

        private const int MaxChainedCloses = 8;

        private bool _closing;
        private bool _hasPending;
        private int _pendingDay;

        public DayCycle(IGameClock clock, IExpenseService expenses,
                        DailyReportService reports, IEventBus events)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _expenses = expenses;
            _reports = reports;
            _events = events;

            _clock.EndOfDayReached += OnEndOfDay;
        }

        public DailyReport LastClosed { get; private set; }

        private void OnEndOfDay(int day)
        {
            if (_closing)
            {
                _hasPending = true;
                _pendingDay = day;
                return;
            }

            _closing = true;

            try
            {
                for (int i = 0; i < MaxChainedCloses; i++)
                {
                    _hasPending = false;

                    CloseOneDay(day);

                    if (!_hasPending) return;
                    day = _pendingDay;
                }

                UnityEngine.Debug.LogError(
                    $"[DayCycle] {MaxChainedCloses} days closed in sequence in a " +
                    "single call. A DayStarted subscriber is constantly requesting the end of the " +
                    "day — chaining was interrupted.");
            }
            finally
            {
                _closing = false;
                _hasPending = false;
            }
        }

        private void CloseOneDay(int day)
        {
            Money expenseTotal = Money.Zero;
            IReadOnlyList<ExpenseLine> lines = null;

            if (_expenses != null)
            {

                lines = new List<ExpenseLine>(_expenses.Preview());

                _reports?.BeginExpenseCharge();
                try { expenseTotal = _expenses.ChargeDay(); }
                finally { _reports?.EndExpenseCharge(); }
            }

            LastClosed = _reports?.Close(day + 1, lines, expenseTotal);

            _events?.Publish(new DayEnded(day));

            _clock.AdvanceDay();
        }

        public void Dispose() => _clock.EndOfDayReached -= OnEndOfDay;
    }
}
