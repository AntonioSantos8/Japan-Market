using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Data
{
    /// <summary>
    /// Personalidade de compra de um cliente. Um asset por arquétipo.
    ///
    /// Ter isto como dado, e não como campos no prefab do NPC, é o que permite
    /// "cliente apressado", "cliente pechincha" e "cliente que compra muito"
    /// serem três assets em vez de três prefabs quase idênticos que divergem
    /// com o tempo.
    /// </summary>
    [CreateAssetMenu(fileName = "CustomerProfile",
        menuName = "Japan Market/Customer Profile", order = 30)]
    public sealed class CustomerProfileData : ScriptableObject
    {
        [Header("Compras")]
        [Tooltip("Quantos móveis diferentes ele pretende visitar.")]
        [SerializeField] private Vector2Int _shelvesToVisit = new(1, 4);

        [Tooltip("Quantas unidades ele pega por prateleira.")]
        [SerializeField] private Vector2Int _unitsPerShelf = new(1, 3);

        [Tooltip("Teto de itens na cesta, somando tudo.")]
        [Min(1)] [SerializeField] private int _basketCapacity = 6;

        [Header("Tolerância")]
        [Tooltip("Múltiplo do preço de mercado que ele aceita pagar. " +
                 "1.5 = topa pagar até 50% acima da referência.")]
        [Min(1f)] [SerializeField] private float _priceToleranceMultiplier = 1.5f;

        [Tooltip("Sujeiras ativas a partir das quais ele desiste e vai embora.")]
        [Min(1)] [SerializeField] private int _dirtTolerance = 12;

        [Tooltip("Segundos esperando na fila antes de desistir. 0 = espera para sempre.")]
        [Min(0f)] [SerializeField] private float _queuePatience = 90f;

        [Header("Ritmo")]
        [Tooltip("Segundos parado escolhendo, em cada prateleira.")]
        [SerializeField] private Vector2 _browseDuration = new(1.5f, 3.5f);

        [Tooltip("Segundos parado logo após entrar, antes de começar a comprar.")]
        [SerializeField] private Vector2 _entryDelay = new(0.5f, 2f);

        [SerializeField] private float _moveSpeed = 2.6f;
        [SerializeField] private float _turnSpeedDegrees = 480f;

        [Header("Pagamento")]
        [Range(0f, 1f)]
        [Tooltip("Chance de pagar com cartão. O resto paga em dinheiro.")]
        [SerializeField] private float _cardPaymentChance = 0.5f;

        // ── leitura ──────────────────────────────────────────────────────────

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

        /// <summary>Preço máximo que este cliente aceita por uma unidade.</summary>
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
