using System;
using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;
using JapanMarket.Domain;
using UnityEngine;

namespace JapanMarket.Tests
{

    public sealed class FakeFurniture : IFurniture, ICapabilityProvider
    {
        private readonly Dictionary<Type, IFurnitureCapability> _capabilities = new();

        public FurnitureId Id { get; } = FurnitureId.Generate();
        public FurnitureDefinition Definition => null;
        public Vector3 Position { get; set; }
        public Quaternion Rotation => Quaternion.identity;
        public bool IsAlive { get; set; } = true;

        public FakeFurniture With<T>(T capability) where T : class, IFurnitureCapability
        {
            _capabilities[typeof(T)] = capability;

            if (capability is IOwnedTestCapability owned) owned.Owner = this;

            return this;
        }

        public bool TryGetCapability<T>(out T capability) where T : class, IFurnitureCapability
        {
            if (_capabilities.TryGetValue(typeof(T), out IFurnitureCapability found))
            {
                capability = (T)found;
                return true;
            }

            capability = null;
            return false;
        }

        public bool HasCapability<T>() where T : class, IFurnitureCapability =>
            _capabilities.ContainsKey(typeof(T));

        public IEnumerable<KeyValuePair<Type, IFurnitureCapability>> EnumerateCapabilities() =>
            _capabilities;
    }
}
