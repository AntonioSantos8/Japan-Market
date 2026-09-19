using System;
using System.Collections;
using System.Collections.Generic;
using JapanMarket.Core;

namespace JapanMarket.Domain
{

    public sealed class FurnitureRegistry : IFurnitureRegistry
    {
        private readonly List<IFurniture> _all = new();
        private readonly Dictionary<FurnitureId, IFurniture> _byId = new();
        private readonly Dictionary<Type, IList> _byCapability = new();

        public IReadOnlyList<IFurniture> All => _all;

        public event Action<IFurniture> Placed;
        public event Action<IFurniture> Removing;

        public IReadOnlyList<T> WithCapability<T>() where T : class, IFurnitureCapability
        {
            return _byCapability.TryGetValue(typeof(T), out IList list)
                ? (List<T>)list
                : (IReadOnlyList<T>)Array.Empty<T>();
        }

        public bool TryGetById(FurnitureId id, out IFurniture furniture) =>
            _byId.TryGetValue(id, out furniture) && furniture.IsAlive;

        public bool TryFind<T>(Func<T, bool> predicate, out T capability)
            where T : class, IFurnitureCapability
        {
            IReadOnlyList<T> candidates = WithCapability<T>();

            for (int i = 0; i < candidates.Count; i++)
            {
                T candidate = candidates[i];
                if (candidate?.Owner == null || !candidate.Owner.IsAlive) continue;
                if (predicate != null && !predicate(candidate)) continue;

                capability = candidate;
                return true;
            }

            capability = null;
            return false;
        }

        public void Register(IFurniture furniture)
        {
            if (furniture == null || _all.Contains(furniture)) return;

            _all.Add(furniture);

            if (furniture.Id.IsValid) _byId[furniture.Id] = furniture;

            foreach (KeyValuePair<Type, IFurnitureCapability> entry in EnumerateCapabilities(furniture))
                GetOrCreateList(entry.Key).Add(entry.Value);

            Placed?.Invoke(furniture);
        }

        public void Unregister(IFurniture furniture)
        {
            if (furniture == null || !_all.Contains(furniture)) return;

            Removing?.Invoke(furniture);

            _all.Remove(furniture);

            if (furniture.Id.IsValid && _byId.TryGetValue(furniture.Id, out IFurniture stored)
                && ReferenceEquals(stored, furniture))
                _byId.Remove(furniture.Id);

            foreach (KeyValuePair<Type, IFurnitureCapability> entry in EnumerateCapabilities(furniture))
                if (_byCapability.TryGetValue(entry.Key, out IList list))
                    list.Remove(entry.Value);
        }

        public void Clear()
        {

            IFurniture[] snapshot = _all.ToArray();
            foreach (IFurniture furniture in snapshot) Unregister(furniture);

            _all.Clear();
            _byId.Clear();
            _byCapability.Clear();
        }

        private static IEnumerable<KeyValuePair<Type, IFurnitureCapability>> EnumerateCapabilities(
            IFurniture furniture)
        {
            if (furniture is ICapabilityProvider provider) return provider.EnumerateCapabilities();
            return Array.Empty<KeyValuePair<Type, IFurnitureCapability>>();
        }

        private IList GetOrCreateList(Type capabilityType)
        {
            if (_byCapability.TryGetValue(capabilityType, out IList existing)) return existing;

            IList created = (IList)Activator.CreateInstance(
                typeof(List<>).MakeGenericType(capabilityType));

            _byCapability.Add(capabilityType, created);
            return created;
        }
    }

    public interface ICapabilityProvider
    {
        IEnumerable<KeyValuePair<Type, IFurnitureCapability>> EnumerateCapabilities();
    }
}
