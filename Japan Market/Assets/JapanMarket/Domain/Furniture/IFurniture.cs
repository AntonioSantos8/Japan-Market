using JapanMarket.Core;
using JapanMarket.Data;
using UnityEngine;

namespace JapanMarket.Domain
{

    public interface IFurniture
    {
        FurnitureId Id { get; }
        FurnitureDefinition Definition { get; }

        Vector3 Position { get; }
        Quaternion Rotation { get; }

        bool IsAlive { get; }

        bool TryGetCapability<T>(out T capability) where T : class, IFurnitureCapability;
        bool HasCapability<T>() where T : class, IFurnitureCapability;
    }
}
