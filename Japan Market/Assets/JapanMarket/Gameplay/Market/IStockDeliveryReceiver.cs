using JapanMarket.Data;

namespace JapanMarket.Gameplay
{
    public interface IStockDeliveryReceiver
    {
        void InitializeDelivery(ItemDefinition product);
    }
}
