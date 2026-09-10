using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Data
{
    /// <summary>
    /// Tudo o que o jogo sabe sobre um produto. Um asset por produto.
    ///
    /// Substitui o <c>AllIThingsData</c>, que descrevia produto, móvel E expansão
    /// de loja no mesmo tipo — com metade dos campos sem sentido em cada uso.
    /// Aqui o tipo descreve uma coisa só, e o que não se aplica não existe.
    ///
    /// Nada aqui é preço de venda: preço é decisão do jogador, muda todo dia e
    /// pertence ao IPricingService, não à definição imutável do produto.
    /// </summary>
    [CreateAssetMenu(fileName = "Product", menuName = "Japan Market/Product", order = 0)]
    public sealed class ItemDefinition : ScriptableObject
    {
        [Header("Identidade")]
        [SerializeField, ReadOnlyField]
        [Tooltip("Gerado uma vez, na criação. Só é usado em save e telemetria — " +
                 "em runtime, refira-se a este asset diretamente.")]
        private ProductId _id;

        [SerializeField] private LocalizedText _displayName;
        [SerializeField] private LocalizedText _description;

        [Header("Classificação")]
        [SerializeField] private ProductCategory _category;

        [Tooltip("Condição de armazenamento exigida. O móvel precisa fornecer este trait.")]
        [SerializeField] private StorageTrait _requiredStorage;

        [Header("Comércio")]
        [Tooltip("Quanto o jogador paga por unidade ao comprar do fornecedor.")]
        [SerializeField] private Money _baseCost;

        [Tooltip("Referência de mercado. O jogador vê e decide o preço dele em cima disso.")]
        [SerializeField] private Money _marketPrice;

        [Min(1)] [SerializeField] private int _unitsPerBox = 8;

        [Header("Apresentação")]
        [SerializeField] private GameObject _itemPrefab;
        [SerializeField] private GameObject _boxPrefab;
        [SerializeField] private Sprite _icon;

        [Header("Disposição")]
        [SerializeField] private ItemGrid _shelfGrid = new();
        [SerializeField] private ItemGrid _boxGrid = new();

        [Header("Progressão")]
        [Tooltip("Vazio = disponível desde o começo.")]
        [SerializeField] private UnlockCondition _unlock;

        // ── migração ─────────────────────────────────────────────────────────
        [SerializeField, HideInInspector]
        [Tooltip("Valor do enum Items de origem. Existe só para a migração poder " +
                 "reescrever cenas e prefabs, e sai do projeto na Fase 8.")]
        private int _legacyEnumValue = -1;

        // ── leitura ──────────────────────────────────────────────────────────
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

        /// <summary>Custo de uma caixa fechada: custo unitário × unidades.</summary>
        public Money BoxCost => _baseCost * _unitsPerBox;

        /// <summary>Este produto cabe num móvel que fornece estes traits?</summary>
        public bool FitsStorage(StorageTrait provided) =>
            _requiredStorage == null || _requiredStorage == provided;

#if UNITY_EDITOR
        /// <summary>Só o editor escreve identidade. Nunca chamado em runtime.</summary>
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
            // Um id vazio num asset já criado significa duplicação por Ctrl+D ou
            // um asset feito à mão. Gerar aqui evita dois produtos com o mesmo id
            // chegarem ao catálogo.
            if (!_id.IsValid)
            {
                _id = ProductId.Generate();
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
#endif
    }
}
