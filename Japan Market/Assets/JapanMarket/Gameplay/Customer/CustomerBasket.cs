using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;
using UnityEngine;

namespace JapanMarket.Gameplay
{

    [DisallowMultipleComponent]
    public sealed class CustomerBasket : MonoBehaviour
    {
        public readonly struct Entry
        {
            public readonly ItemDefinition Product;
            public readonly Money PricePaid;

            public Entry(ItemDefinition product, Money pricePaid)
            {
                Product = product;
                PricePaid = pricePaid;
            }
        }

        private readonly List<Entry> _entries = new();

        public IReadOnlyList<Entry> Entries => _entries;
        public int Count => _entries.Count;
        public bool IsEmpty => _entries.Count == 0;

        public Money Total
        {
            get
            {
                Money total = Money.Zero;
                for (int i = 0; i < _entries.Count; i++) total += _entries[i].PricePaid;
                return total;
            }
        }

        public void Add(ItemDefinition product, Money pricePaid)
        {
            if (product == null) return;
            _entries.Add(new Entry(product, pricePaid));
        }

        public bool TryTakeFirst(out Entry entry)
        {
            if (_entries.Count == 0) { entry = default; return false; }

            entry = _entries[0];
            _entries.RemoveAt(0);
            return true;
        }

        public void Clear() => _entries.Clear();
    }
}
