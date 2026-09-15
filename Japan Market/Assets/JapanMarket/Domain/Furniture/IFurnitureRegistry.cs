using System;
using System.Collections.Generic;

namespace JapanMarket.Domain
{

    public interface IFurnitureRegistry
    {
        IReadOnlyList<IFurniture> All { get; }

        IReadOnlyList<T> WithCapability<T>() where T : class, IFurnitureCapability;

        bool TryGetById(Core.FurnitureId id, out IFurniture furniture);

        event Action<IFurniture> Placed;

        event Action<IFurniture> Removing;

        void Register(IFurniture furniture);
        void Unregister(IFurniture furniture);
    }
}
