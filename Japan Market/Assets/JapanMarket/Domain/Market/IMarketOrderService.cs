using System;
using JapanMarket.Core;

namespace JapanMarket.Domain
{
    public interface IMarketOrderService
    {
        DeliveryQueue DeliveryQueue { get; }

        bool TryCheckout(MarketCart cart);

        event Action<MarketCart> OrderPlaced;
    }
}

