using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Data
{

    [CreateAssetMenu(fileName = "Furniture", menuName = "Japan Market/Furniture", order = 2)]
    public sealed class FurnitureDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField, ReadOnlyField] private FurnitureId _id;
        [SerializeField] private LocalizedText _displayName;
        [SerializeField] private LocalizedText _description;
        [SerializeField] private FurnitureCategory _category;

        [Header("Trade")]
        [SerializeField] private Money _price;

        [Tooltip("How much the player receives when selling it back. Zero = not sellable.")]
        [SerializeField] private Money _resaleValue;

        [Header("Prefabs")]
        [SerializeField] private GameObject _prefab;
        [SerializeField] private GameObject _ghostPrefab;
        [SerializeField] private Sprite _icon;

        [Header("Positioning")]
        [Tooltip("Space occupied on the store grid, in cells.")]
        [SerializeField] private Vector2Int _footprint = Vector2Int.one;

        [Tooltip("Base height relative to the floor. Was the legacy 'floorDistance'.")]
        [SerializeField] private float _floorOffset;

        [Tooltip("Degrees per rotation step. 90 = four orientations; 0 = does not rotate.")]
        [SerializeField] private float _rotationStep = 90f;

        [Tooltip("Can be placed against the wall? Used by positioning validation.")]
        [SerializeField] private bool _wallMounted;

        [Header("Operation")]
        [Tooltip("Daily energy cost while turned on. Zero = consumes nothing. " +
                 "The PowerConsumer component in the prefab reads this value.")]
        [SerializeField] private Money _dailyPowerCost;

        [Header("Progression")]
        [Tooltip("Empty = available from the start.")]
        [SerializeField] private UnlockCondition _unlock;

        public FurnitureId Id => _id;
        public LocalizedText DisplayName => _displayName;
        public LocalizedText Description => _description;
        public FurnitureCategory Category => _category;
        public Money Price => _price;
        public Money ResaleValue => _resaleValue;
        public GameObject Prefab => _prefab;
        public GameObject GhostPrefab => _ghostPrefab;
        public Sprite Icon => _icon;
        public Vector2Int Footprint => _footprint;
        public float FloorOffset => _floorOffset;
        public float RotationStep => _rotationStep;
        public bool WallMounted => _wallMounted;
        public Money DailyPowerCost => _dailyPowerCost;
        public UnlockCondition Unlock => _unlock;

        public bool CanRotate => _rotationStep > 0.01f;
        public bool IsSellable => _resaleValue > Money.Zero;

        public bool IsUnlocked(IUnlockContext context) =>
            _unlock == null || _unlock.IsSatisfied(context);

#if UNITY_EDITOR
        public void EditorInitialize(FurnitureId id) => _id = id;

        private void OnValidate()
        {
            if (!_id.IsValid)
            {
                _id = FurnitureId.Generate();
                UnityEditor.EditorUtility.SetDirty(this);
            }

            if (_footprint.x < 1) _footprint.x = 1;
            if (_footprint.y < 1) _footprint.y = 1;
        }
#endif
    }
}
