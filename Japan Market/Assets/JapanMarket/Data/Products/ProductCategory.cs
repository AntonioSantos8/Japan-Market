using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Data
{

    [CreateAssetMenu(fileName = "Category", menuName = "Japan Market/Product Category", order = 20)]
    public sealed class ProductCategory : ScriptableObject
    {
        [SerializeField] private LocalizedText _displayName;
        [SerializeField] private Sprite _icon;
        [SerializeField] private Color _tint = Color.white;

        [Tooltip("Display order in the computer catalog. Lower appears first.")]
        [SerializeField] private int _sortOrder;

        public LocalizedText DisplayName => _displayName;
        public Sprite Icon => _icon;
        public Color Tint => _tint;
        public int SortOrder => _sortOrder;
    }
}
