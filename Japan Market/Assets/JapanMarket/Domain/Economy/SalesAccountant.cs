using System;
using JapanMarket.Core;

namespace JapanMarket.Domain
{

    public sealed class SalesAccountant : IDisposable
    {
        private readonly ILedger _ledger;
        private IDisposable _subscription;

        public SalesAccountant(IEventBus events, ILedger ledger)
        {
            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));

            if (events == null) return;
            _subscription = events.Subscribe<SaleCompleted>(OnSale);
        }

        private void OnSale(SaleCompleted sale)
        {
            if (!sale.Revenue.IsPositive) return;

            _ledger.Deposit(sale.Revenue, TransactionReason.ProductSale,
                            $"{sale.ItemCount} item(s)");
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _subscription = null;
        }
    }
}
