using UnityEngine;

namespace JapanMarket.Domain
{

    public interface ICustomer
    {
        int Id { get; }
        Vector3 Position { get; }
        bool IsAlive { get; }

        void Notify(CustomerSignal signal);
    }

    public enum CustomerSignal
    {

        StoreClosed = 0,

        CheckoutLost = 1,

        SaleFinished = 3,
    }
}
