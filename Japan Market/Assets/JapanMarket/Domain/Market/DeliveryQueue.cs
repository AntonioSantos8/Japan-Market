using System;
using System.Collections.Generic;
using JapanMarket.Data;

namespace JapanMarket.Domain
{
    public sealed class DeliveryBox
    {
        public ItemDefinition Product { get; }

        public DeliveryBox(ItemDefinition product)
        {
            Product = product ?? throw new ArgumentNullException(nameof(product));
        }
    }

    public sealed class DeliveryQueue
    {
        private readonly Queue<DeliveryBox> _boxes = new();

        public int PendingBoxes => _boxes.Count;

        public event Action<DeliveryBox> BoxEnqueued;
        public event Action<DeliveryBox> BoxDequeued;

        public void Enqueue(ItemDefinition product, int count)
        {
            if (product == null || count <= 0) return;

            for (int i = 0; i < count; i++)
            {
                var box = new DeliveryBox(product);
                _boxes.Enqueue(box);
                BoxEnqueued?.Invoke(box);
            }
        }

        public bool TryDequeue(out DeliveryBox box)
        {
            if (_boxes.Count > 0)
            {
                box = _boxes.Dequeue();
                BoxDequeued?.Invoke(box);
                return true;
            }

            box = null;
            return false;
        }

        public void Restore(IEnumerable<DeliveryBox> boxes)
        {
            _boxes.Clear();
            if (boxes != null)
            {
                foreach (var box in boxes)
                    _boxes.Enqueue(box);
            }
        }
    }
}

