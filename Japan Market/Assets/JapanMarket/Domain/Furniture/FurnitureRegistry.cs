using System;
using System.Collections;
using System.Collections.Generic;
using JapanMarket.Core;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Implementação do <see cref="IFurnitureRegistry"/>. C# puro, sem Unity
    /// além dos tipos de dado — roda inteiro em teste de unidade.
    ///
    /// A listagem por capacidade é mantida incrementalmente: registrar um móvel
    /// custa um passo por capacidade que ele tem, e consultar custa zero. O
    /// contrário — filtrar a lista toda a cada consulta — é o que o código atual
    /// faz em <c>GetPlacedFurnitures()</c>, e com dezenas de móveis e vários NPCs
    /// perguntando por frame isso aparece no profiler.
    /// </summary>
    public sealed class FurnitureRegistry : IFurnitureRegistry
    {
        private readonly List<IFurniture> _all = new();
        private readonly Dictionary<FurnitureId, IFurniture> _byId = new();
        private readonly Dictionary<Type, IList> _byCapability = new();

        public IReadOnlyList<IFurniture> All => _all;

        public event Action<IFurniture> Placed;
        public event Action<IFurniture> Removing;

        // ── consulta ─────────────────────────────────────────────────────────

        public IReadOnlyList<T> WithCapability<T>() where T : class, IFurnitureCapability
        {
            return _byCapability.TryGetValue(typeof(T), out IList list)
                ? (List<T>)list
                : (IReadOnlyList<T>)Array.Empty<T>();
        }

        public bool TryGetById(FurnitureId id, out IFurniture furniture) =>
            _byId.TryGetValue(id, out furniture) && furniture.IsAlive;

        /// <summary>
        /// Primeiro móvel vivo com a capacidade que satisfaça o filtro.
        /// Não aloca — o filtro recebe a capacidade direto.
        /// </summary>
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

        // ── ciclo de vida ────────────────────────────────────────────────────

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

            // O aviso sai ANTES de qualquer remoção: quem está usando o móvel
            // ainda precisa conseguir ler a capacidade para se desligar dela.
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
            // Snapshot antes de iterar: Unregister dispara Removing, e um handler
            // que desregistre OUTRO móvel encolheria a lista embaixo do laço.
            IFurniture[] snapshot = _all.ToArray();
            foreach (IFurniture furniture in snapshot) Unregister(furniture);

            _all.Clear();
            _byId.Clear();
            _byCapability.Clear();
        }

        // ── internos ─────────────────────────────────────────────────────────

        /// <summary>
        /// O móvel expõe as capacidades que tem; o registro só as indexa. Isso
        /// mantém a reflexão do lado do Gameplay (uma vez por tipo de prefab) e
        /// deixa Domain livre dela.
        /// </summary>
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

    /// <summary>
    /// Implementado pelo móvel para dizer ao registro sob quais interfaces ele
    /// deve ser indexado. Separado de <see cref="IFurniture"/> porque é detalhe
    /// de registro, não algo que os consumidores devam ver.
    /// </summary>
    public interface ICapabilityProvider
    {
        IEnumerable<KeyValuePair<Type, IFurnitureCapability>> EnumerateCapabilities();
    }
}
