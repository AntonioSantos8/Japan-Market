using System.Collections.Generic;
using UnityEngine;

namespace JapanMarket.Data
{
    /// <summary>A fonte única de verdade sobre que lixo existe.</summary>
    public interface ITrashCatalog
    {
        IReadOnlyList<TrashDefinition> All { get; }
        IReadOnlyList<TrashCategory> Categories { get; }

        /// <summary>Acha a categoria pela chave do save. Null quando sumiu do projeto.</summary>
        TrashCategory FindCategory(string key);

        /// <summary>Acha o lixo pela chave do save. Null quando sumiu do projeto.</summary>
        TrashDefinition FindItem(string key);

        /// <summary>
        /// Sorteia um lixo respeitando o peso de cada um. Null com o catálogo
        /// vazio ou com todos os pesos em zero.
        /// </summary>
        TrashDefinition PickRandom();
    }

    /// <summary>
    /// Catálogo de lixo. UM asset no projeto inteiro, populado pelo import.
    ///
    /// Substitui o array <c>trashDatas</c> que o <c>TrashSystem</c> tinha no
    /// Inspector: aquela lista era a única fonte, e um lixo criado e esquecido
    /// fora dela simplesmente nunca aparecia no jogo, sem erro nenhum.
    /// </summary>
    [CreateAssetMenu(fileName = "TrashCatalog",
        menuName = "Japan Market/Trash/Catalog", order = 72)]
    public sealed class TrashCatalog : ScriptableObject,
        ITrashCatalog, IEditableCatalog<TrashDefinition>
    {
        [SerializeField] private List<TrashDefinition> _items = new();

        private Dictionary<string, TrashCategory> _byKey;
        private Dictionary<string, TrashDefinition> _itemsByKey;
        private List<TrashCategory> _categories;
        private float _totalWeight;

        public IReadOnlyList<TrashDefinition> All => _items;
        public string CatalogName => "Lixo";
        public int EntryCount => _items.Count;

        public IReadOnlyList<TrashCategory> Categories { get { EnsureBuilt(); return _categories; } }

        private void OnEnable() => Rebuild();

        public TrashCategory FindCategory(string key)
        {
            EnsureBuilt();

            return string.IsNullOrWhiteSpace(key)
                ? null
                : _byKey.TryGetValue(key.Trim(), out TrashCategory category) ? category : null;
        }

        public TrashDefinition FindItem(string key)
        {
            EnsureBuilt();

            return string.IsNullOrWhiteSpace(key)
                ? null
                : _itemsByKey.TryGetValue(key.Trim(), out TrashDefinition item) ? item : null;
        }

        /// <summary>
        /// Sorteio por peso, em uma passada. O acumulado é pré-calculado no
        /// Rebuild porque isto roda toda vez que um cliente suja a loja.
        /// </summary>
        public TrashDefinition PickRandom()
        {
            EnsureBuilt();
            if (_totalWeight <= 0f) return null;

            float roll = Random.Range(0f, _totalWeight);

            for (int i = 0; i < _items.Count; i++)
            {
                TrashDefinition item = _items[i];
                if (item == null || !item.IsValid) continue;

                roll -= item.SpawnWeight;
                if (roll <= 0f) return item;
            }

            // Só chega aqui por erro de ponto flutuante na última fração.
            for (int i = _items.Count - 1; i >= 0; i--)
                if (_items[i] != null && _items[i].IsValid && _items[i].SpawnWeight > 0f)
                    return _items[i];

            return null;
        }

        public void Rebuild()
        {
            _byKey = new Dictionary<string, TrashCategory>();
            _itemsByKey = new Dictionary<string, TrashDefinition>();
            _categories = new List<TrashCategory>();
            _totalWeight = 0f;

            for (int i = 0; i < _items.Count; i++)
            {
                TrashDefinition item = _items[i];
                if (item == null || !item.IsValid) continue;

                _totalWeight += item.SpawnWeight;

                // Primeiro a entrar vence; o repetido é reportado pelo Validate.
                if (!_itemsByKey.ContainsKey(item.Key)) _itemsByKey[item.Key] = item;

                TrashCategory category = item.Category;

                // Primeira a entrar vence; a repetida é reportada pelo Validate.
                if (_byKey.ContainsKey(category.Key)) continue;

                _byKey[category.Key] = category;
                _categories.Add(category);
            }

            _categories.Sort((a, b) =>
                a.SortOrder != b.SortOrder
                    ? a.SortOrder.CompareTo(b.SortOrder)
                    : string.CompareOrdinal(a.name, b.name));
        }

        private void EnsureBuilt()
        {
            if (_byKey == null) Rebuild();
        }

        public List<CatalogProblem> Validate()
        {
            // O peso total é calculado no Rebuild, e o validador pode ser o
            // primeiro a tocar no catálogo depois de um import.
            EnsureBuilt();

            var problems = new List<CatalogProblem>();
            var keys = new Dictionary<string, TrashCategory>();
            var itemKeys = new Dictionary<string, TrashDefinition>();

            for (int i = 0; i < _items.Count; i++)
            {
                TrashDefinition item = _items[i];

                if (item == null)
                {
                    problems.Add(new CatalogProblem(null, "Entrada vazia no catálogo."));
                    continue;
                }

                string problem = item.DescribeProblem();
                if (problem != null) problems.Add(new CatalogProblem(item, problem));

                if (!item.HasKey)
                {
                    problems.Add(new CatalogProblem(item,
                        "Sem chave: o save vai usar o nome do asset, e renomear passa " +
                        "a esvaziar os sacos que já estavam com este lixo dentro."));
                }

                if (itemKeys.TryGetValue(item.Key, out TrashDefinition twin))
                {
                    if (twin != item)
                        problems.Add(new CatalogProblem(item,
                            $"Chave '{item.Key}' repetida com '{twin.name}'."));
                }
                else itemKeys[item.Key] = item;

                TrashCategory category = item.Category;
                if (category == null) continue;

                if (!category.HasKey)
                {
                    problems.Add(new CatalogProblem(category,
                        "Categoria sem chave: o save vai usar o nome do asset, e " +
                        "renomear passa a apagar os sacos pendentes."));
                }

                if (keys.TryGetValue(category.Key, out TrashCategory other))
                {
                    if (other != category)
                    {
                        problems.Add(new CatalogProblem(category,
                            $"Chave '{category.Key}' repetida com '{other.name}'. O save " +
                            "não conseguiria distinguir as duas."));
                    }

                    continue;
                }

                keys[category.Key] = category;
            }

            if (_totalWeight <= 0f && _items.Count > 0)
            {
                problems.Add(new CatalogProblem(this,
                    "Todos os pesos de spawn estão em zero: nenhum lixo apareceria na loja."));
            }

            return problems;
        }

#if UNITY_EDITOR
        public bool EditorSetItems(List<TrashDefinition> items)
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
