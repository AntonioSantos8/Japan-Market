using System;
using System.Collections.Generic;

namespace JapanMarket.Domain
{

    public sealed class CheckoutQueue
    {
        private readonly List<ICustomer> _customers = new();

        public event Action<ICustomer, int> IndexChanged;

        public int Count => _customers.Count;
        public bool IsEmpty => _customers.Count == 0;
        public ICustomer Front => _customers.Count > 0 ? _customers[0] : null;

        public IReadOnlyList<ICustomer> Customers => _customers;

        public bool Contains(ICustomer customer) => IndexOf(customer) >= 0;

        public int IndexOf(ICustomer customer)
        {
            if (customer == null) return -1;

            for (int i = 0; i < _customers.Count; i++)
                if (ReferenceEquals(_customers[i], customer)) return i;

            return -1;
        }

        public bool IsFront(ICustomer customer) =>
            customer != null && ReferenceEquals(Front, customer);

        public int Enqueue(ICustomer customer)
        {
            if (customer == null) return -1;
            if (Contains(customer)) return IndexOf(customer);

            _customers.Add(customer);
            int index = _customers.Count - 1;

            IndexChanged?.Invoke(customer, index);
            return index;
        }

        public bool Remove(ICustomer customer)
        {
            int index = IndexOf(customer);
            if (index < 0) return false;

            _customers.RemoveAt(index);

            for (int i = index; i < _customers.Count; i++)
                IndexChanged?.Invoke(_customers[i], i);

            return true;
        }

        public void DisbandAll()
        {
            if (_customers.Count == 0) return;

            ICustomer[] leaving = _customers.ToArray();
            _customers.Clear();

            for (int i = 0; i < leaving.Length; i++)
                leaving[i]?.Notify(CustomerSignal.CheckoutLost);
        }

        public bool PruneDead()
        {
            int firstRemoved = -1;

            for (int i = _customers.Count - 1; i >= 0; i--)
            {
                if (_customers[i] != null && _customers[i].IsAlive) continue;

                _customers.RemoveAt(i);
                firstRemoved = i;
            }

            if (firstRemoved < 0) return false;

            for (int i = firstRemoved; i < _customers.Count; i++)
                IndexChanged?.Invoke(_customers[i], i);

            return true;
        }
    }
}
