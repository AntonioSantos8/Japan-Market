using JapanMarket.Core;
using JapanMarket.Domain;

namespace JapanMarket.Tests
{

    public interface IOwnedTestCapability : IFurnitureCapability
    {
        new IFurniture Owner { get; set; }
    }

    public sealed class FakePowerConsumer : IOwnedTestCapability, IPowerConsumer
    {
        public FakePowerConsumer(long dailyYen, bool poweredOn = true)
        {
            DailyCost = Money.FromYen(dailyYen);
            IsPoweredOn = poweredOn;
        }

        public IFurniture Owner { get; set; }

        public Money DailyCost { get; set; }
        public bool IsPoweredOn { get; private set; }

        public void SetPowered(bool on) => IsPoweredOn = on;
    }
}
