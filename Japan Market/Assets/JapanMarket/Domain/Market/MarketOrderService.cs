using System;
using JapanMarket.Core;

namespace JapanMarket.Domain
{
    public sealed class MarketOrderService : IMarketOrderService
    {
        private readonly ILedger _ledger;
        private readonly IPricingService _pricing;

        public DeliveryQueue DeliveryQueue { get; } = new();

        public event Action<MarketCart> OrderPlaced;

        public MarketOrderService(ILedger ledger, IPricingService pricing)
        {
            _ledger = ledger;
            _pricing = pricing;
        }

        public bool TryCheckout(MarketCart cart)
        {
            if (cart == null || cart.TotalBoxes == 0) return false;
            if (_ledger == null) return false;

            Money totalCost = cart.TotalCost;

            if (!_ledger.TryWithdraw(totalCost, TransactionReason.StockPurchase, "Stock Purchase"))
            {
                return false;
            }

            foreach (var kvp in cart.Items)
            {
                var product = kvp.Key;
                int boxes = kvp.Value;

                DeliveryQueue.Enqueue(product, boxes);

                Money unitCost = product.BaseCost;
                int totalUnits = boxes * product.UnitsPerBox;

                _pricing?.RecordRestock(product, unitCost, totalUnits);
            }

            OrderPlaced?.Invoke(cart);

            return true;
        }
    }
}
