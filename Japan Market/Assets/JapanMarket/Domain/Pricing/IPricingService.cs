using System;
using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;

namespace JapanMarket.Domain
{
    public readonly struct PricingData
    {
        public readonly ItemDefinition Product;
        public readonly Money SellPrice;

        public readonly Money LastCost;
        public readonly Money CurrentCost;
        public readonly Money AverageCost;
        public readonly int UnitsInAverage;

        public readonly int LastChangeDay;

        public Money Profit => SellPrice - CurrentCost;

        public PricingData(ItemDefinition product, Money sellPrice, Money lastCost, Money currentCost, Money averageCost, int unitsInAverage, int lastChangeDay)
        {
            Product = product;
            SellPrice = sellPrice;
            LastCost = lastCost;
            CurrentCost = currentCost;
            AverageCost = averageCost;
            UnitsInAverage = unitsInAverage;
            LastChangeDay = lastChangeDay;
        }

        public static PricingData Initial(ItemDefinition product)
        {
            return new PricingData(product, product.MarketPrice, Money.Zero, product.BaseCost, Money.Zero, 0, 1);
        }

        public PricingData WithSellPrice(Money price, int day) =>
            new PricingData(Product, price, LastCost, CurrentCost, AverageCost, UnitsInAverage, day);

        public PricingData WithRestock(Money unitCost, int quantity)
        {
            if (quantity <= 0) return this;

            Money newAverage;
            if (UnitsInAverage == 0)
                newAverage = unitCost;
            else
            {
                long totalYen = (AverageCost.Yen * UnitsInAverage) + (unitCost.Yen * quantity);
                newAverage = Money.FromYen(totalYen / (UnitsInAverage + quantity));
            }

            Money lastCost = (CurrentCost == unitCost) ? LastCost : CurrentCost;

            return new PricingData(Product, SellPrice, lastCost, unitCost, newAverage, UnitsInAverage + quantity, LastChangeDay);
        }
    }

    public interface IPricingService
    {
        Money GetSellPrice(ItemDefinition product);
        PricingData GetPricingData(ItemDefinition product);
        bool HasCustomPrice(ItemDefinition product);

        void SetSellPrice(ItemDefinition product, Money price, int day);
        void ClearSellPrice(ItemDefinition product, int day);

        void RecordRestock(ItemDefinition product, Money unitCost, int quantity);

        event Action<ItemDefinition, Money> PriceChanged;
    }

    public sealed class PricingService : IPricingService
    {
        private readonly Dictionary<ItemDefinition, PricingData> _data = new();

        public event Action<ItemDefinition, Money> PriceChanged;

        public Money GetSellPrice(ItemDefinition product)
        {
            if (product == null) return Money.Zero;
            return _data.TryGetValue(product, out var data) ? data.SellPrice : product.MarketPrice;
        }

        public PricingData GetPricingData(ItemDefinition product)
        {
            if (product == null) return default;
            return _data.TryGetValue(product, out var data) ? data : PricingData.Initial(product);
        }

        public bool HasCustomPrice(ItemDefinition product) =>
            product != null && _data.ContainsKey(product);

        public void SetSellPrice(ItemDefinition product, Money price, int day)
        {
            if (product == null) return;

            PricingData current = GetPricingData(product);
            _data[product] = current.WithSellPrice(price, day);

            PriceChanged?.Invoke(product, price);
        }

        public void ClearSellPrice(ItemDefinition product, int day)
        {
            if (product == null) return;

            PricingData current = GetPricingData(product);
            _data[product] = current.WithSellPrice(product.MarketPrice, day);

            PriceChanged?.Invoke(product, product.MarketPrice);
        }

        public void RecordRestock(ItemDefinition product, Money unitCost, int quantity)
        {
            if (product == null || quantity <= 0) return;

            PricingData current = GetPricingData(product);
            _data[product] = current.WithRestock(unitCost, quantity);
        }

        public void Restore(IEnumerable<PricingData> savedData)
        {
            _data.Clear();
            if (savedData != null)
            {
                foreach (var data in savedData)
                    _data[data.Product] = data;
            }
        }
    }
}
