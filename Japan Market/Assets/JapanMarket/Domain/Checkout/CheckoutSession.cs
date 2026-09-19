using System;
using System.Collections.Generic;
using JapanMarket.Core;

namespace JapanMarket.Domain
{

    public sealed class CheckoutSession
    {
        private readonly List<SaleLine> _lines;
        private readonly List<SaleLine> _scanned = new();

        public CheckoutSession(ICustomer customer, IReadOnlyList<SaleLine> lines,
                               PaymentMethod method)
        {
            Customer = customer ?? throw new ArgumentNullException(nameof(customer));
            _lines = new List<SaleLine>(lines ?? throw new ArgumentNullException(nameof(lines)));
            Method = method;

            Total = Money.Zero;
            for (int i = 0; i < _lines.Count; i++) Total += _lines[i].Price;
        }

        public ICustomer Customer { get; }
        public IReadOnlyList<SaleLine> Lines => _lines;
        public IReadOnlyList<SaleLine> Scanned => _scanned;
        public PaymentMethod Method { get; }

        public Money Total { get; }

        public Money ScannedTotal { get; private set; }

        public int ScannedCount => _scanned.Count;
        public int PendingCount => _lines.Count - _scanned.Count;
        public bool AllScanned => _scanned.Count >= _lines.Count;
        public bool IsComplete { get; private set; }

        public Money AmountTendered { get; private set; }

        public event Action<CheckoutSession, SaleLine> LineScanned;
        public event Action<CheckoutSession> AllLinesScanned;

        public bool TryScanNext(out SaleLine line)
        {
            line = default;
            if (IsComplete || AllScanned) return false;

            line = _lines[_scanned.Count];
            _scanned.Add(line);
            ScannedTotal += line.Price;

            LineScanned?.Invoke(this, line);
            if (AllScanned) AllLinesScanned?.Invoke(this);

            return true;
        }

        public void SetAmountTendered(Money amount) => AmountTendered = amount;

        public Money ChangeDue => Method == PaymentMethod.Card
            ? Money.Zero
            : Money.Max(Money.Zero, AmountTendered - Total);

        internal void MarkComplete() => IsComplete = true;

        public override string ToString() =>
            $"Sale of {_lines.Count} item(s), {Total}, {Method}, " +
            $"{(IsComplete ? "completed" : $"{ScannedCount}/{_lines.Count} scanned")}";
    }
}
