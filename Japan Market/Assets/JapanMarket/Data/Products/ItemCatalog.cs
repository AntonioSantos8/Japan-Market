using System.Collections.Generic;
using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Data
{
    /// <summary>
    /// Catálogo de produtos. UM asset no projeto inteiro.
    ///
    /// A lista não é arrastada à mão: um AssetPostprocessor a reconstrói sempre
    /// que um ItemDefinition é criado, movido ou deletado. Cadastrar um produto
    /// passa a ser criar um asset — e nada mais.
    ///
    /// Os índices são construídos no OnEnable e viram consultas O(1). O código
    /// atual faz foreach linear em todo lugar (GetItemPrefab, GetMarketPrice,
    /// RegisterScannedItem), o que com 100 produtos custa caro por checkout.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemCatalog", menuName = "Japan Market/Item Catalog", order = 1)]
    public sealed class ItemCatalog : ScriptableObject,
        IItemCatalog, IEditableCatalog<ItemDefinition>
    {
        [SerializeField] private List<ItemDefinition> _items = new();

        private Dictionary<ProductId, ItemDefinition> _byId;
        private Dictionary<ProductCategory, List<ItemDefinition>> _byCategory;
        private Dictionary<StorageTrait, List<ItemDefinition>> _byStorage;
        private List<ProductCategory> _categories;

        private static readonly ItemDefinition[] EmptyItems = System.Array.Empty<ItemDefinition>();

        public IReadOnlyList<ItemDefinition> All => _items;
        public string CatalogName => "Produtos";
        public int EntryCount => _items.Count;
        public IReadOnlyList<ProductCategory> Categories { get { EnsureBuilt(); return _categories; } }

        private void OnEnable() => Rebuild();

        // ── consultas ────────────────────────────────────────────────────────

        public bool TryGet(ProductId id, out ItemDefinition definition)
        {
            EnsureBuilt();
            return _byId.TryGetValue(id, out definition);
        }

        public ItemDefinition Get(ProductId id)
        {
            EnsureBuilt();
            if (_byId.TryGetValue(id, out ItemDefinition found)) return found;

            Debug.LogError($"[ItemCatalog] Produto '{id}' não existe no catálogo. " +
                           "Um save antigo ou um asset deletado costuma ser a causa.");
            return null;
        }

        public bool Contains(ProductId id)
        {
            EnsureBuilt();
            return _byId.ContainsKey(id);
        }

        public IReadOnlyList<ItemDefinition> ByCategory(ProductCategory category)
        {
            EnsureBuilt();
            if (category == null) return EmptyItems;
            return _byCategory.TryGetValue(category, out List<ItemDefinition> list)
                ? list : (IReadOnlyList<ItemDefinition>)EmptyItems;
        }

        public IReadOnlyList<ItemDefinition> ByStorage(StorageTrait trait)
        {
            EnsureBuilt();
            if (trait == null) return EmptyItems;
            return _byStorage.TryGetValue(trait, out List<ItemDefinition> list)
                ? list : (IReadOnlyList<ItemDefinition>)EmptyItems;
        }

        public List<ItemDefinition> UnlockedFor(IUnlockContext context)
        {
            EnsureBuilt();
            var result = new List<ItemDefinition>(_items.Count);
            foreach (ItemDefinition item in _items)
                if (item != null && item.IsUnlocked(context)) result.Add(item);
            return result;
        }

        /// <summary>Busca por nome, para o campo "Procurar…" do computador.</summary>
        public void Search(string query, List<ItemDefinition> results)
        {
            EnsureBuilt();
            results.Clear();
            if (string.IsNullOrWhiteSpace(query)) { results.AddRange(_items); return; }

            foreach (ItemDefinition item in _items)
            {
                if (item == null) continue;
                string name = item.DisplayName.Value;
                if (!string.IsNullOrEmpty(name) &&
                    name.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    results.Add(item);
            }
        }

        // ── construção dos índices ───────────────────────────────────────────

        private void EnsureBuilt()
        {
            if (_byId == null) Rebuild();
        }

        public void Rebuild()
        {
            _byId       = new Dictionary<ProductId, ItemDefinition>(_items.Count);
            _byCategory = new Dictionary<ProductCategory, List<ItemDefinition>>();
            _byStorage  = new Dictionary<StorageTrait, List<ItemDefinition>>();
            _categories = new List<ProductCategory>();

            foreach (ItemDefinition item in _items)
            {
                if (item == null) continue;

                if (item.Id.IsValid && !_byId.TryAdd(item.Id, item))
                {
                    Debug.LogError($"[ItemCatalog] Id duplicado em '{item.name}' " +
                                   $"({item.Id}). O primeiro asset com esse id prevalece; " +
                                   "regenere o id do duplicado antes de continuar.", item);
                }

                if (item.Category != null)
                {
                    if (!_byCategory.TryGetValue(item.Category, out List<ItemDefinition> byCat))
                    {
                        byCat = new List<ItemDefinition>();
                        _byCategory.Add(item.Category, byCat);
                        _categories.Add(item.Category);
                    }
                    byCat.Add(item);
                }

                if (item.RequiredStorage != null)
                {
                    if (!_byStorage.TryGetValue(item.RequiredStorage, out List<ItemDefinition> byStore))
                    {
                        byStore = new List<ItemDefinition>();
                        _byStorage.Add(item.RequiredStorage, byStore);
                    }
                    byStore.Add(item);
                }
            }

            _categories.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
        }

        // ── validação ────────────────────────────────────────────────────────

        /// <summary>
        /// Roda no import e no menu do editor. A ideia é que um produto mal
        /// configurado apareça na hora, e não semanas depois como "esse item
        /// vende por ¥0" ou "esse item não spawna no caixa".
        /// </summary>
        public List<CatalogProblem> Validate()
        {
            var problems = new List<CatalogProblem>();
            var seenIds = new HashSet<ProductId>();

            for (int i = 0; i < _items.Count; i++)
            {
                ItemDefinition item = _items[i];

                if (item == null)
                {
                    problems.Add(new CatalogProblem(this, $"Entrada {i} está vazia."));
                    continue;
                }

                if (!item.Id.IsValid)
                    problems.Add(new CatalogProblem(item, "Sem id. Reabra o asset para gerar um."));
                else if (!seenIds.Add(item.Id))
                    problems.Add(new CatalogProblem(item, $"Id duplicado: {item.Id}"));

                if (item.DisplayName.IsEmpty)
                    problems.Add(new CatalogProblem(item, "Sem nome em nenhum idioma."));

                if (item.Category == null)
                    problems.Add(new CatalogProblem(item, "Sem categoria — não vai aparecer no catálogo filtrado."));

                if (item.ItemPrefab == null)
                    problems.Add(new CatalogProblem(item, "Sem prefab de item — não pode ser colocado na prateleira."));

                if (item.BoxPrefab == null)
                    problems.Add(new CatalogProblem(item, "Sem prefab de caixa — não pode ser entregue."));

                if (item.BaseCost <= Money.Zero)
                    problems.Add(new CatalogProblem(item, "Custo base zero ou negativo."));

                if (item.MarketPrice <= Money.Zero)
                    problems.Add(new CatalogProblem(item, "Preço de mercado zero ou negativo."));

                if (item.MarketPrice <= item.BaseCost)
                    problems.Add(new CatalogProblem(item,
                        $"Preço de mercado ({item.MarketPrice}) não é maior que o custo " +
                        $"({item.BaseCost}) — este produto dá prejuízo pelo preço de referência."));

                if (!item.ShelfGrid.IsValid)
                    problems.Add(new CatalogProblem(item, "Grid de prateleira com capacidade zero."));

                if (!item.BoxGrid.IsValid)
                    problems.Add(new CatalogProblem(item, "Grid de caixa com capacidade zero."));
            }

            return problems;
        }

#if UNITY_EDITOR
        /// <summary>Chamado pelo AssetPostprocessor. Devolve true se mudou algo.</summary>
        public bool EditorSetItems(List<ItemDefinition> items)
        {
            if (_items.Count == items.Count)
            {
                bool same = true;
                for (int i = 0; i < items.Count; i++)
                    if (_items[i] != items[i]) { same = false; break; }
                if (same) return false;
            }

            _items = items;
            Rebuild();
            return true;
        }
#endif
    }
}
