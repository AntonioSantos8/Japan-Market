using System;
using JapanMarket.Data;
using UnityEngine;

namespace JapanMarket.Domain
{

    public interface IProductStorage : IFurnitureCapability
    {

        StorageTrait ProvidedStorage { get; }

        ItemDefinition CurrentProduct { get; }

        int Count { get; }

        int Capacity { get; }

        bool IsEmpty { get; }
        bool IsFull { get; }

        bool Accepts(ItemDefinition product);

        bool TryPlace(ItemDefinition product, out int slotIndex);

        bool TryTakeOne(out ItemDefinition product);

        Vector3 GetSlotWorldPosition(int slotIndex);

        event Action<IProductStorage> ContentsChanged;
    }
}
