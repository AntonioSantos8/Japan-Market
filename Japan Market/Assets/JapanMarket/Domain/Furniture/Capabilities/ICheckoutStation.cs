using System;
using System.Collections.Generic;
using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Domain
{

    public interface ICheckoutStation : IFurnitureCapability
    {
        CheckoutStationState State { get; }

        bool IsOperational { get; }

        bool AcceptsNewCustomers { get; }

        int QueueLength { get; }

        Vector3 QueueAnchor { get; }
        Vector3 QueueDirection { get; }
        float QueueSpacing { get; }

        Vector3 CounterPosition { get; }

        Vector3 GetQueuePosition(int index);

        int GetQueueIndex(ICustomer customer);

        bool IsFront(ICustomer customer);

        bool TryJoinQueue(ICustomer customer, out int index);

        void LeaveQueue(ICustomer customer);

        CheckoutSession CurrentSession { get; }

        bool TryOpenSession(ICustomer customer, IReadOnlyList<SaleLine> lines,
                            PaymentMethod method, out CheckoutSession session);

        void CloseSession(CheckoutSession session, SessionCloseReason reason);

        event Action<ICheckoutStation> StateChanged;

        event Action<ICheckoutStation, CheckoutSession> SessionOpened;

        event Action<ICheckoutStation, CheckoutSession, SessionCloseReason> SessionClosed;
    }

    public enum CheckoutStationState
    {

        Idle = 0,

        Waiting = 1,

        Serving = 2,

        Unavailable = 3,
    }

    public enum SessionCloseReason
    {

        Completed = 0,

        Abandoned = 1,

        StationLost = 2,
    }
}
