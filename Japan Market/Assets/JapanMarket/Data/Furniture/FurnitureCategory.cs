using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Data
{
    /// <summary>
    /// Categoria de móvel — prateleiras, refrigeração, caixas, decoração,
    /// equipamento. Aba do catálogo do computador.
    /// </summary>
    [CreateAssetMenu(fileName = "FurnitureCategory",
        menuName = "Japan Market/Furniture Category", order = 22)]
    public sealed class FurnitureCategory : ScriptableObject
    {
        [SerializeField] private LocalizedText _displayName;
        [SerializeField] private Sprite _icon;
        [SerializeField] private int _sortOrder;

        public LocalizedText DisplayName => _displayName;
        public Sprite Icon => _icon;
        public int SortOrder => _sortOrder;
    }
}
