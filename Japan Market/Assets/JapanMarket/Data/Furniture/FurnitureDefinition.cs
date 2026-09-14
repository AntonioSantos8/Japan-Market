using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Data
{
    /// <summary>
    /// Tudo o que o jogo sabe sobre um MODELO de móvel. Um asset por modelo.
    ///
    /// Note o que NÃO está aqui: nada sobre o que o móvel faz. Não há campo
    /// "é prateleira", "é caixa", "capacidade de itens". Comportamento vem dos
    /// componentes de capacidade no prefab, e é por isso que "Prateleira
    /// Quádrupla", "Frigobar" e "Vitrine Refrigerada" são o mesmo código com
    /// prefabs diferentes.
    ///
    /// Substitui o <c>FurnitureData</c> atual, que tinha um <c>FurnitureType</c>
    /// enum de quatro valores e ainda referenciava um <c>AllIThingsData</c> —
    /// dois assets descrevendo a mesma coisa, livres para divergir.
    /// </summary>
    [CreateAssetMenu(fileName = "Furniture", menuName = "Japan Market/Furniture", order = 2)]
    public sealed class FurnitureDefinition : ScriptableObject
    {
        [Header("Identidade")]
        [SerializeField, ReadOnlyField] private FurnitureId _id;
        [SerializeField] private LocalizedText _displayName;
        [SerializeField] private LocalizedText _description;
        [SerializeField] private FurnitureCategory _category;

        [Header("Comércio")]
        [SerializeField] private Money _price;

        [Tooltip("Quanto o jogador recebe ao vender de volta. Zero = não vendável.")]
        [SerializeField] private Money _resaleValue;

        [Header("Prefabs")]
        [SerializeField] private GameObject _prefab;
        [SerializeField] private GameObject _ghostPrefab;
        [SerializeField] private Sprite _icon;

        [Header("Posicionamento")]
        [Tooltip("Espaço ocupado no grid da loja, em células.")]
        [SerializeField] private Vector2Int _footprint = Vector2Int.one;

        [Tooltip("Altura da base em relação ao chão. Era o 'floorDistance' do legado.")]
        [SerializeField] private float _floorOffset;

        [Tooltip("Graus por passo de rotação. 90 = quatro orientações; 0 = não gira.")]
        [SerializeField] private float _rotationStep = 90f;

        [Tooltip("Pode ser encostado na parede? Usado pela validação de posicionamento.")]
        [SerializeField] private bool _wallMounted;

        [Header("Operação")]
        [Tooltip("Custo diário de energia enquanto ligado. Zero = não consome. " +
                 "O componente PowerConsumer no prefab lê este valor.")]
        [SerializeField] private Money _dailyPowerCost;

        [Header("Progressão")]
        [Tooltip("Vazio = disponível desde o começo.")]
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
