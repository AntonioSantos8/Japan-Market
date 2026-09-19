using System;
using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;
using JapanMarket.Domain;
using UnityEngine;

namespace JapanMarket.Gameplay
{

    [DisallowMultipleComponent]
    public sealed class FurnitureInstance : MonoBehaviour, IFurniture, ICapabilityProvider
    {
        [SerializeField] private FurnitureDefinition _definition;

        [Tooltip("Desligue para móveis de cenário que não devem ser descobertos " +
                 "por NPC nem contados em despesas.")]
        [SerializeField] private bool _registerInStore = true;

        private readonly Dictionary<Type, IFurnitureCapability> _capabilities = new();
        private readonly List<IFurnitureCapability> _capabilityComponents = new();

        private FurnitureId _runtimeId;
        private IFurnitureRegistry _registry;
        private bool _alive;
        private Vector3 _lastKnownPosition;
        private Quaternion _lastKnownRotation = Quaternion.identity;

        private static bool _missingContextWarned;

        public FurnitureDefinition Definition => _definition;

        public FurnitureId Id => _runtimeId;

        public bool IsAlive => _alive && this != null;

        public Vector3 Position => IsAlive ? transform.position : _lastKnownPosition;
        public Quaternion Rotation => IsAlive ? transform.rotation : _lastKnownRotation;

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

        public IEnumerable<KeyValuePair<Type, IFurnitureCapability>> EnumerateCapabilities()
        {
            foreach (KeyValuePair<Type, IFurnitureCapability> pair in _capabilities)
                yield return pair;
        }

        private void Awake()
        {
            if (!_runtimeId.IsValid) _runtimeId = FurnitureId.Generate();
            CollectCapabilities();
        }

        private void OnEnable()
        {
            _alive = true;
            CacheTransform();

            if (!_registerInStore) return;

            _registry = ResolveRegistry();
            _registry?.Register(this);
        }

        private void OnDisable()
        {

            CacheTransform();

            _registry?.Unregister(this);
            _registry = null;

            _alive = false;
        }

        private void CacheTransform()
        {
            if (this == null) return;
            _lastKnownPosition = transform.position;
            _lastKnownRotation = transform.rotation;
        }

        private IFurnitureRegistry ResolveRegistry()
        {
            GameContext context = GameContext.Current;
            if (context != null && context.Services.TryResolve(out IFurnitureRegistry registry))
                return registry;

            if (!_missingContextWarned)
            {
                _missingContextWarned = true;
                Debug.LogWarning(
                    "[FurnitureInstance] Não há GameContext nesta cena, então os móveis " +
                    "não entram no registro — NPCs e despesas não vão encontrá-los. " +
                    "Adicione um objeto com GameContext à cena.", this);
            }
            return null;
        }

        private void CollectCapabilities()
        {
            _capabilities.Clear();
            _capabilityComponents.Clear();

            GetComponentsInChildren(true, _capabilityComponents);

            foreach (IFurnitureCapability capability in _capabilityComponents)
            {
                if (capability == null) continue;

                foreach (Type contract in GetCapabilityContracts(capability.GetType()))
                {
                    if (_capabilities.TryAdd(contract, capability)) continue;

                    Debug.LogWarning(
                        $"[FurnitureInstance] '{name}' tem dois componentes expondo " +
                        $"'{contract.Name}'. O primeiro prevalece; remova o duplicado.", this);
                }
            }
        }

        private static readonly Dictionary<Type, Type[]> ContractCache = new();

        private static Type[] GetCapabilityContracts(Type componentType)
        {
            if (ContractCache.TryGetValue(componentType, out Type[] cached)) return cached;

            var contracts = new List<Type>(2);
            foreach (Type contract in componentType.GetInterfaces())
            {
                if (contract == typeof(IFurnitureCapability)) continue;
                if (!typeof(IFurnitureCapability).IsAssignableFrom(contract)) continue;
                contracts.Add(contract);
            }

            Type[] result = contracts.ToArray();
            ContractCache.Add(componentType, result);
            return result;
        }

        public void SetDefinition(FurnitureDefinition definition) => _definition = definition;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_definition == null)
                Debug.LogWarning($"[FurnitureInstance] '{name}' sem FurnitureDefinition. " +
                                 "Preço, categoria e desbloqueio ficam indefinidos.", this);
        }
#endif
    }
}
