using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Data
{

    [CreateAssetMenu(fileName = "Product", menuName = "Japan Market/Product", order = 0)]
    public sealed class ItemDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField, ReadOnlyField]
        [Tooltip("Generated once, on creation. Only used in save and telemetry — " +
                 "at runtime, refer to this asset directly.")]
        private ProductId _id;

        [SerializeField] private LocalizedText _displayName;
        [SerializeField] private LocalizedText _description;

        [Header("Classification")]
        [SerializeField] private ProductCategory _category;

        [Tooltip("Required storage condition. The furniture must provide this trait.")]
        [SerializeField] private StorageTrait _requiredStorage;

        [Header("Trade")]
        [Tooltip("How much the player pays per unit when buying from the supplier.")]
        [SerializeField] private Money _baseCost;

        [Tooltip("Market reference. The player sees and decides their price based on this.")]
        [SerializeField] private Money _marketPrice;

        [Min(1)] [SerializeField] private int _unitsPerBox = 8;

        [Header("Presentation")]
        [SerializeField] private GameObject _itemPrefab;
        [SerializeField] private GameObject _boxPrefab;
        [SerializeField] private Sprite _icon;

        [Header("Layout")]
        [SerializeField] private ItemGrid _shelfGrid = new();
        [SerializeField] private ItemGrid _boxGrid = new();

        [Header("Progression")]
        [Tooltip("Empty = available from the start.")]
        [SerializeField] private UnlockCondition _unlock;

        [SerializeField, HideInInspector]
        [Tooltip("Source Items enum value. Exists only so the migration can " +
                 "rewrite scenes and prefabs, and leaves the project in Phase 8.")]
        private int _legacyEnumValue = -1;

        public ProductId Id => _id;
        public LocalizedText DisplayName => _displayName;
        public LocalizedText Description => _description;
        public ProductCategory Category => _category;
        public StorageTrait RequiredStorage => _requiredStorage;
        public Money BaseCost => _baseCost;
        public Money MarketPrice => _marketPrice;
        public int UnitsPerBox => _unitsPerBox;
        public GameObject ItemPrefab => _itemPrefab;
        public GameObject BoxPrefab => _boxPrefab;
        public Sprite Icon => _icon;
        public ItemGrid ShelfGrid => _shelfGrid;
        public ItemGrid BoxGrid => _boxGrid;
        public UnlockCondition Unlock => _unlock;
        public int LegacyEnumValue => _legacyEnumValue;

        public bool IsUnlocked(IUnlockContext context) =>
            _unlock == null || _unlock.IsSatisfied(context);

        public Money BoxCost => _baseCost * _unitsPerBox;

        public bool FitsStorage(StorageTrait provided) =>
            _requiredStorage == null || _requiredStorage == provided;

#if UNITY_EDITOR

        public void EditorInitialize(ProductId id, int legacyEnumValue)
        {
            _id = id;
            _legacyEnumValue = legacyEnumValue;
        }

        public void EditorSetContent(
            LocalizedText displayName, LocalizedText description,
            Money baseCost, Money marketPrice,
            GameObject itemPrefab, GameObject boxPrefab, Sprite icon)
        {
            _displayName = displayName;
            _description = description;
            _baseCost    = baseCost;
            _marketPrice = marketPrice;
            _itemPrefab  = itemPrefab;
            _boxPrefab   = boxPrefab;
            _icon        = icon;
        }

        private void OnValidate()
        {

            if (!_id.IsValid)
            {
                _id = ProductId.Generate();
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
#endif
    }
}
