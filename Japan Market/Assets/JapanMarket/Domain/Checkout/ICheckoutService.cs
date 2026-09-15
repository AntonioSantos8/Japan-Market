using System;
using UnityEngine;

namespace JapanMarket.Domain
{

    public interface ICheckoutService
    {

        bool TryFindBestStation(Vector3 from, Predicate<Vector3> canReach,
                                out ICheckoutStation station);

        bool TryCompleteSale(ICheckoutStation station);

        event Action<CheckoutSession> SaleCompleted;
    }
}
