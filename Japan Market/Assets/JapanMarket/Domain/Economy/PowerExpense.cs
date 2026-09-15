using System.Collections.Generic;
using JapanMarket.Core;

namespace JapanMarket.Domain
{

    public sealed class PowerExpense : IExpenseSource
    {
        private readonly IFurnitureRegistry _furniture;

        public PowerExpense(IFurnitureRegistry furniture, string label = "Electricity")
        {
            _furniture = furniture;
            Label = label;
        }

        public string Label { get; }
        public TransactionReason Reason => TransactionReason.Electricity;

        public Money GetDailyCost()
        {
            if (_furniture == null) return Money.Zero;

            IReadOnlyList<IPowerConsumer> consumers =
                _furniture.WithCapability<IPowerConsumer>();

            Money total = Money.Zero;

            for (int i = 0; i < consumers.Count; i++)
            {
                IPowerConsumer consumer = consumers[i];

                if (consumer?.Owner == null || !consumer.Owner.IsAlive) continue;
                if (!consumer.IsPoweredOn) continue;

                total += consumer.DailyCost;
            }

            return total;
        }

        public int PoweredDeviceCount
        {
            get
            {
                if (_furniture == null) return 0;

                IReadOnlyList<IPowerConsumer> consumers =
                    _furniture.WithCapability<IPowerConsumer>();

                int count = 0;
                for (int i = 0; i < consumers.Count; i++)
                {
                    IPowerConsumer consumer = consumers[i];
                    if (consumer?.Owner == null || !consumer.Owner.IsAlive) continue;
                    if (consumer.IsPoweredOn) count++;
                }

                return count;
            }
        }
    }
}
