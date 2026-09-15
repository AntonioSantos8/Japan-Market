using System;
using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;

namespace JapanMarket.Domain
{
    public sealed class MarketCart
    {
        private readonly Dictionary<ItemDefinition, int> _items = new();

        public IReadOnlyDictionary<ItemDefinition, int> Items => _items;

        public Money TotalCost
        {
            get
            {
                Money total = Money.Zero;
                foreach (var kvp in _items)
                {
                    Money boxCost = kvp.Key.BoxCost;
                    long costYen = boxCost.Yen * kvp.Value;
                    total += Money.FromYen(costYen);
                }
                return total;
            }
        }

        public int TotalBoxes
        {
            get
            {
                int total = 0;
                foreach (var qty in _items.Values) total += qty;
                return total;
            }
        }

        public void AddBoxes(ItemDefinition product, int boxes)
        {
            if (product == null || boxes <= 0) return;

            _items.TryGetValue(product, out int current);
            _items[product] = current + boxes;
        }

        public void RemoveBoxes(ItemDefinition product, int boxes)
        {
            if (product == null || boxes <= 0) return;

            if (_items.TryGetValue(product, out int current))
            {
                int newValue = current - boxes;
                if (newValue <= 0)
                    _items.Remove(product);
                else
                    _items[product] = newValue;
            }
        }

        public void SetBoxes(ItemDefinition product, int boxes)
        {
            if (product == null) return;

            if (boxes <= 0)
                _items.Remove(product);
            else
                _items[product] = boxes;
        }

        public void Clear() => _items.Clear();
    }
}

