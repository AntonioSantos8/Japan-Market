using UnityEngine;

namespace JapanMarket.Core
{

    public readonly struct BalanceChanged : IGameEvent
    {
        public readonly Money Previous;
        public readonly Money Current;
        public readonly TransactionReason Reason;

        public Money Delta => Current - Previous;

        public BalanceChanged(Money previous, Money current, TransactionReason reason)
        {
            Previous = previous;
            Current  = current;
            Reason   = reason;
        }
    }

    public readonly struct TransactionRecorded : IGameEvent
    {
        public readonly Money Amount;               
        public readonly TransactionReason Reason;
        public readonly int Day;

        public TransactionRecorded(Money amount, TransactionReason reason, int day)
        {
            Amount = amount;
            Reason = reason;
            Day    = day;
        }
    }

    public readonly struct DayStarted : IGameEvent
    {
        public readonly int Day;
        public DayStarted(int day) => Day = day;
    }

    public readonly struct DayEnded : IGameEvent
    {
        public readonly int Day;
        public DayEnded(int day) => Day = day;
    }

    public readonly struct StoreOpenStateChanged : IGameEvent
    {
        public readonly bool IsOpen;
        public StoreOpenStateChanged(bool isOpen) => IsOpen = isOpen;
    }

    public readonly struct CustomerEntered : IGameEvent
    {
        public readonly int CustomerId;
        public readonly Transform Transform;

        public CustomerEntered(int customerId, Transform transform)
        {
            CustomerId = customerId;
            Transform  = transform;
        }
    }

    public readonly struct CustomerLeft : IGameEvent
    {
        public readonly int CustomerId;
        public readonly bool WasSatisfied;
        public readonly CustomerLeaveReason Reason;

        public CustomerLeft(int customerId, bool wasSatisfied, CustomerLeaveReason reason)
        {
            CustomerId   = customerId;
            WasSatisfied = wasSatisfied;
            Reason       = reason;
        }
    }

    public enum CustomerLeaveReason
    {
        Purchased        = 0,
        NothingToBuy     = 1,
        StoreTooDirty    = 2,
        PricesTooHigh    = 3,
        NoCheckout       = 4,
        WaitedTooLong    = 5,
        StoreClosed      = 6,
    }

    public readonly struct SaleCompleted : IGameEvent
    {
        public readonly int CustomerId;
        public readonly FurnitureId StationId;

        public readonly Money Revenue;

        public readonly Money Cost;

        public readonly int ItemCount;
        public readonly PaymentMethod Method;

        public Money Profit => Revenue - Cost;

        public SaleCompleted(int customerId, FurnitureId stationId, Money revenue,
                             Money cost, int itemCount, PaymentMethod method)
        {
            CustomerId = customerId;
            StationId  = stationId;
            Revenue    = revenue;
            Cost       = cost;
            ItemCount  = itemCount;
            Method     = method;
        }
    }

    public readonly struct CleanlinessChanged : IGameEvent
    {
        public readonly int ActiveDirtCount;
        public readonly float Normalized;   

        public CleanlinessChanged(int activeDirtCount, float normalized)
        {
            ActiveDirtCount = activeDirtCount;
            Normalized      = normalized;
        }
    }
}
