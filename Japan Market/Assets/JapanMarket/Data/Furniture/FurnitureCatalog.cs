using System.Collections.Generic;
using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Data
{

    [CreateAssetMenu(fileName = "FurnitureCatalog",
        menuName = "Japan Market/Furniture Catalog", order = 3)]
    public sealed class FurnitureCatalog : ScriptableObject,
        IFurnitureCatalog, IEditableCatalog<FurnitureDefinition>
    {
        [SerializeField] private List<FurnitureDefinition> _items = new();

        private Dictionary<FurnitureId, FurnitureDefinition> _byId;
        private Dictionary<FurnitureCategory, List<FurnitureDefinition>> _byCategory;
        private List<FurnitureCategory> _categories;

        private static readonly FurnitureDefinition[] Empty =
            System.Array.Empty<FurnitureDefinition>();

        public IReadOnlyList<FurnitureDefinition> All => _items;
        public string CatalogName => "Furniture";
        public int EntryCount => _items.Count;

        public IReadOnlyList<FurnitureCategory> Categories
        {
            get { EnsureBuilt(); return _categories; }
        }

        private void OnEnable() => Rebuild();

        public bool TryGet(FurnitureId id, out FurnitureDefinition definition)
        {
            EnsureBuilt();
            return _byId.TryGetValue(id, out definition);
        }

        public FurnitureDefinition Get(FurnitureId id)
        {
            EnsureBuilt();
            if (_byId.TryGetValue(id, out FurnitureDefinition found)) return found;

            Debug.LogError($"[FurnitureCatalog] Furniture '{id}' does not exist in the catalog. " +
                           "Old save or deleted asset is usually the cause.");
            return null;
        }

        public bool Contains(FurnitureId id)
        {
            EnsureBuilt();
            return _byId.ContainsKey(id);
        }

        public IReadOnlyList<FurnitureDefinition> ByCategory(FurnitureCategory category)
        {
            EnsureBuilt();
            if (category == null) return Empty;
            return _byCategory.TryGetValue(category, out List<FurnitureDefinition> list)
                ? list : (IReadOnlyList<FurnitureDefinition>)Empty;
        }

        public List<FurnitureDefinition> UnlockedFor(IUnlockContext context)
        {
            EnsureBuilt();
            var result = new List<FurnitureDefinition>(_items.Count);
            foreach (FurnitureDefinition item in _items)
                if (item != null && item.IsUnlocked(context)) result.Add(item);
            return result;
        }

        private void EnsureBuilt()
        {
            if (_byId == null) Rebuild();
        }

        public void Rebuild()
        {
            _byId       = new Dictionary<FurnitureId, FurnitureDefinition>(_items.Count);
            _byCategory = new Dictionary<FurnitureCategory, List<FurnitureDefinition>>();
            _categories = new List<FurnitureCategory>();

            foreach (FurnitureDefinition item in _items)
            {
                if (item == null) continue;

                if (item.Id.IsValid && !_byId.TryAdd(item.Id, item))
                {
                    Debug.LogError($"[FurnitureCatalog] Duplicated id in '{item.name}' " +
                                   $"({item.Id}). Regenerate the id for the duplicate.", item);
                }

                if (item.Category == null) continue;

                if (!_byCategory.TryGetValue(item.Category, out List<FurnitureDefinition> list))
                {
                    list = new List<FurnitureDefinition>();
                    _byCategory.Add(item.Category, list);
                    _categories.Add(item.Category);
                }
                list.Add(item);
            }

            _categories.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
        }

        public List<CatalogProblem> Validate()
        {
            var problems = new List<CatalogProblem>();
            var seen = new HashSet<FurnitureId>();

            for (int i = 0; i < _items.Count; i++)
            {
                FurnitureDefinition item = _items[i];

                if (item == null)
                {
                    problems.Add(new CatalogProblem(this, $"Entry {i} is empty."));
                    continue;
                }

                if (!item.Id.IsValid)
                    problems.Add(new CatalogProblem(item, "No id. Reopen the asset to generate one."));
                else if (!seen.Add(item.Id))
                    problems.Add(new CatalogProblem(item, $"Duplicated id: {item.Id}"));

                if (item.DisplayName.IsEmpty)
                    problems.Add(new CatalogProblem(item, "No name in any language."));

                if (item.Category == null)
                    problems.Add(new CatalogProblem(item, "No category — won't appear in the filtered catalog."));

                if (item.Prefab == null)
                    problems.Add(new CatalogProblem(item, "No prefab — cannot be placed in the store."));

                if (item.GhostPrefab == null)
                    problems.Add(new CatalogProblem(item, "No ghost — build mode has nothing to show."));

                if (item.Price <= Money.Zero)
                    problems.Add(new CatalogProblem(item, "Zero or negative price."));

                if (item.ResaleValue > item.Price)
                    problems.Add(new CatalogProblem(item,
                        $"Resale ({item.ResaleValue}) greater than price ({item.Price}) — " +
                        "the player profits by buying and selling in a loop."));
            }

            return problems;
        }

#if UNITY_EDITOR
        public bool EditorSetItems(List<FurnitureDefinition> items)
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
