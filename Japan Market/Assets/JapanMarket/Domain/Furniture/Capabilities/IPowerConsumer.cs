using JapanMarket.Core;

namespace JapanMarket.Domain
{

    public interface IPowerConsumer : IFurnitureCapability
    {

        Money DailyCost { get; }

        bool IsPoweredOn { get; }

        void SetPowered(bool on);
    }
}
