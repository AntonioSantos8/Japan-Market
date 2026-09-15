using System.Collections.Generic;
using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Data
{

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
        public string CatalogName => "Products";
        public int EntryCount => _items.Count;
        public IReadOnlyList<ProductCategory> Categories { get { EnsureBuilt(); return _categories; } }

        private void OnEnable() => Rebuild();

        public bool TryGet(ProductId id, out ItemDefinition definition)
        {
            EnsureBuilt();
            return _byId.TryGetValue(id, out definition);
        }

        public ItemDefinition Get(ProductId id)
        {
            EnsureBuilt();
            if (_byId.TryGetValue(id, out ItemDefinition found)) return found;

            Debug.LogError($"[ItemCatalog] Product '{id}' does not exist in the catalog. " +
                           "An old save or a deleted asset is usually the cause.");
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
                    Debug.LogError($"[ItemCatalog] Duplicated id in '{item.name}' " +
                                   $"({item.Id}). The first asset with this id prevails; " +
                                   "regenerate the id for the duplicate before continuing.", item);
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

        public List<CatalogProblem> Validate()
        {
            var problems = new List<CatalogProblem>();
            var seenIds = new HashSet<ProductId>();

            for (int i = 0; i < _items.Count; i++)
            {
                ItemDefinition item = _items[i];

                if (item == null)
                {
                    problems.Add(new CatalogProblem(this, $"Entry {i} is empty."));
                    continue;
                }

                if (!item.Id.IsValid)
                    problems.Add(new CatalogProblem(item, "No id. Reopen the asset to generate one."));
                else if (!seenIds.Add(item.Id))
                    problems.Add(new CatalogProblem(item, $"Duplicated id: {item.Id}"));

                if (item.DisplayName.IsEmpty)
                    problems.Add(new CatalogProblem(item, "No name in any language."));

                if (item.Category == null)
                    problems.Add(new CatalogProblem(item, "No category — won't appear in the filtered catalog."));

                if (item.ItemPrefab == null)
                    problems.Add(new CatalogProblem(item, "No item prefab — cannot be placed on the shelf."));

                if (item.BoxPrefab == null)
                    problems.Add(new CatalogProblem(item, "No box prefab — cannot be delivered."));

                if (item.BaseCost <= Money.Zero)
                    problems.Add(new CatalogProblem(item, "Zero or negative base cost."));

                if (item.MarketPrice <= Money.Zero)
                    problems.Add(new CatalogProblem(item, "Zero or negative market price."));

                if (item.MarketPrice <= item.BaseCost)
                    problems.Add(new CatalogProblem(item,
                        $"Market price ({item.MarketPrice}) is not greater than the cost " +
                        $"({item.BaseCost}) — this product takes a loss at the reference price."));

                if (!item.ShelfGrid.IsValid)
                    problems.Add(new CatalogProblem(item, "Shelf grid with zero capacity."));

                if (!item.BoxGrid.IsValid)
                    problems.Add(new CatalogProblem(item, "Box grid with zero capacity."));
            }

            return problems;
        }

#if UNITY_EDITOR

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
