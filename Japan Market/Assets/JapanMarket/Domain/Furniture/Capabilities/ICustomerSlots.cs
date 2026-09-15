using System;
using UnityEngine;

namespace JapanMarket.Domain
{

    public interface ICustomerSlots : IFurnitureCapability
    {
        int Capacity { get; }
        int FreeCount { get; }
        bool HasFreeSlot { get; }

        bool TryReserve(out ISlotReservation reservation);
    }

    public interface ISlotReservation : IDisposable
    {
        int SlotIndex { get; }

        bool IsValid { get; }

        Vector3 WorldPosition { get; }

        Vector3 Facing { get; }
    }
}
