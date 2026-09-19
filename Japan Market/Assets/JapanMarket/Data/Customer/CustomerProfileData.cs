using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Data
{

    [CreateAssetMenu(fileName = "CustomerProfile",
        menuName = "Japan Market/Customer Profile", order = 30)]
    public sealed class CustomerProfileData : ScriptableObject
    {
        [Header("Shopping")]
        [Tooltip("How many different furnitures they intend to visit.")]
        [SerializeField] private Vector2Int _shelvesToVisit = new(1, 4);

        [Tooltip("How many units they take per shelf.")]
        [SerializeField] private Vector2Int _unitsPerShelf = new(1, 3);

        [Tooltip("Maximum items in the basket, total sum.")]
        [Min(1)] [SerializeField] private int _basketCapacity = 6;

        [Header("Tolerance")]
        [Tooltip("Multiple of the market price they accept to pay. " +
                 "1.5 = willing to pay up to 50% above the reference.")]
        [Min(1f)] [SerializeField] private float _priceToleranceMultiplier = 1.5f;

        [Tooltip("Active dirtiness from which they give up and leave.")]
        [Min(1)] [SerializeField] private int _dirtTolerance = 12;

        [Tooltip("Seconds waiting in line before giving up. 0 = wait forever.")]
        [Min(0f)] [SerializeField] private float _queuePatience = 90f;

        [Header("Pacing")]
        [Tooltip("Seconds standing while choosing, on each shelf.")]
        [SerializeField] private Vector2 _browseDuration = new(1.5f, 3.5f);

        [Tooltip("Seconds standing right after entering, before starting to shop.")]
        [SerializeField] private Vector2 _entryDelay = new(0.5f, 2f);

        [SerializeField] private float _moveSpeed = 2.6f;
        [SerializeField] private float _turnSpeedDegrees = 480f;

        [Header("Payment")]
        [Range(0f, 1f)]
        [Tooltip("Chance to pay by card. The rest pays in cash.")]
        [SerializeField] private float _cardPaymentChance = 0.5f;

        public int BasketCapacity => _basketCapacity;
        public float PriceToleranceMultiplier => _priceToleranceMultiplier;
        public int DirtTolerance => _dirtTolerance;
        public float QueuePatience => _queuePatience;
        public float MoveSpeed => _moveSpeed;
        public float TurnSpeedDegrees => _turnSpeedDegrees;

        public int RollShelvesToVisit() => RollInclusive(_shelvesToVisit);
        public int RollUnitsPerShelf() => RollInclusive(_unitsPerShelf);
        public float RollBrowseDuration() => Random.Range(_browseDuration.x, _browseDuration.y);
        public float RollEntryDelay() => Random.Range(_entryDelay.x, _entryDelay.y);
        public bool RollPrefersCard() => Random.value < _cardPaymentChance;

        public Money MaxAcceptablePrice(Money marketPrice) =>
            marketPrice * _priceToleranceMultiplier;

        private static int RollInclusive(Vector2Int range)
        {
            int min = Mathf.Min(range.x, range.y);
            int max = Mathf.Max(range.x, range.y);
            return Random.Range(min, max + 1);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_shelvesToVisit.x < 0) _shelvesToVisit.x = 0;
            if (_unitsPerShelf.x < 1) _unitsPerShelf.x = 1;
            if (_browseDuration.x < 0f) _browseDuration.x = 0f;
            if (_entryDelay.x < 0f) _entryDelay.x = 0f;
        }
#endif
    }
}
