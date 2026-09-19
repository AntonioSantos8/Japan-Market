using System;
using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;

namespace JapanMarket.Tests
{
    /// <summary>
    /// Um catálogo de mentira com a lista que o teste mandar.
    ///
    /// O <see cref="ItemCatalog"/> de verdade é um ScriptableObject que se
    /// popula sozinho no import, e um teste que precisasse dele precisaria de um
    /// projeto Unity aberto com assets em disco. Só o que os serviços consomem
    /// está implementado: o resto lança, para que um uso novo apareça como falha
    /// de teste em vez de passar despercebido devolvendo lista vazia.
    /// </summary>
    public sealed class FakeItemCatalog : IItemCatalog
    {
        private readonly List<ItemDefinition> _all = new();

        public FakeItemCatalog(params ItemDefinition[] products)
        {
            if (products != null) _all.AddRange(products);
        }

        public IReadOnlyList<ItemDefinition> All => _all;

        public void Add(ItemDefinition product) => _all.Add(product);

        public List<ItemDefinition> UnlockedFor(IUnlockContext context)
        {
            var result = new List<ItemDefinition>();

            for (int i = 0; i < _all.Count; i++)
                if (_all[i] != null && _all[i].IsUnlocked(context)) result.Add(_all[i]);

            return result;
        }

        // ── não usados pelos serviços em teste ───────────────────────────────

        public bool TryGet(ProductId id, out ItemDefinition definition) =>
            throw new NotSupportedException();

        public ItemDefinition Get(ProductId id) => throw new NotSupportedException();
        public bool Contains(ProductId id) => throw new NotSupportedException();

        public IReadOnlyList<ItemDefinition> ByCategory(ProductCategory category) =>
            throw new NotSupportedException();

        public IReadOnlyList<ItemDefinition> ByStorage(StorageTrait trait) =>
            throw new NotSupportedException();

        public IReadOnlyList<ProductCategory> Categories => throw new NotSupportedException();
    }
}
