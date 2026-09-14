using System;
using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;
using JapanMarket.Domain;
using UnityEngine;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// Um móvel colocado na loja.
    ///
    /// Repare no que esta classe NÃO faz: não sabe estocar produto, não sabe
    /// atender cliente, não sabe consumir energia. Ela só carrega identidade,
    /// entra no registro e sabe responder "eu tenho a capacidade X?". Todo o
    /// resto vem de componentes no prefab.
    ///
    /// É a diferença direta entre o que existe hoje — <c>FurnitureInstance</c>
    /// com um campo <c>public Shelf shelf</c> pendurado, e um <c>FurnitureType</c>
    /// enum de quatro valores decidindo o comportamento — e um modelo onde
    /// adicionar "Vitrine Refrigerada com leitor de código" é montar um prefab.
    /// </summary>
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

        // ── IFurniture ───────────────────────────────────────────────────────

        public FurnitureDefinition Definition => _definition;

        /// <summary>
        /// Identidade desta instância — única por móvel colocado, não por modelo.
        /// Dois freezers idênticos são dois ids diferentes, porque o save precisa
        /// saber qual deles guardava o quê.
        /// </summary>
        public FurnitureId Id => _runtimeId;

        /// <summary>
        /// Só depende de estado gerenciado, então continua respondendo mesmo
        /// depois de o objeto nativo ser destruído — que é exatamente quando
        /// alguém precisa da resposta.
        /// </summary>
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

        // ── ciclo de vida ────────────────────────────────────────────────────

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
            // A posição é gravada ANTES de sair do registro: quem reage ao evento
            // Removing ainda pode perguntar onde o móvel estava, sem tocar no
            // transform de um objeto que pode já estar destruído.
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

        // ── descoberta de capacidades ────────────────────────────────────────

        /// <summary>
        /// Uma varredura, no Awake, indexando cada componente sob TODAS as
        /// interfaces de capacidade que ele implementa. A partir daí,
        /// <see cref="TryGetCapability{T}"/> é um lookup de dicionário — nunca
        /// um GetComponent, muito menos um GetComponent dentro de Update.
        /// </summary>
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

        /// <summary>
        /// Reflexão uma vez por TIPO de componente, não por instância. Colocar
        /// cem prateleiras na loja custa a mesma reflexão que colocar uma.
        /// </summary>
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

        /// <summary>
        /// Chamado pelo posicionamento ao instanciar o prefab. Fica fora de
        /// #if UNITY_EDITOR de propósito: colocar móvel é ação de jogo.
        /// </summary>
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
