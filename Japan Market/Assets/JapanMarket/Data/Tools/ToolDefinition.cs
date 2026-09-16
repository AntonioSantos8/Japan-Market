using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Data
{
    /// <summary>
    /// Uma ferramenta da roda: esponja, rodo, tablet, engradado, taco.
    ///
    /// O asset descreve a ferramenta; o DESGASTE não mora aqui. Um
    /// ScriptableObject é compartilhado por toda a partida e gravado dentro do
    /// projeto — uma esponja que perdesse durabilidade no asset chegaria gasta
    /// na próxima sessão, e no build chegaria gasta para todo mundo. O estado
    /// vive no <c>ToolSlot</c>, que é de runtime.
    ///
    /// <see cref="WorksOn"/> vazio significa "não trabalha em superfície
    /// nenhuma": é o caso do tablet e do engradado, que são ferramentas de
    /// interação e não de limpeza. Elas ocupam slot, aparecem na roda e são
    /// selecionáveis — só não limpam nada.
    /// </summary>
    [CreateAssetMenu(fileName = "Tool", menuName = "Japan Market/Tool/Tool", order = 81)]
    public sealed class ToolDefinition : ScriptableObject
    {
        [Header("Identificação")]
        [SerializeField] private LocalizedText _displayName;
        [SerializeField] private Sprite _icon;

        [Tooltip("O modelo que aparece na mão do jogador.")]
        [SerializeField] private GameObject _heldPrefab;

        [Header("Uso")]
        [Tooltip("Superfícies em que esta ferramenta trabalha. Vazio = não limpa nada " +
                 "(tablet, engradado).")]
        [SerializeField] private ToolSurface[] _worksOn;

        [Min(0f)]
        [Tooltip("Quanto de sujeira ela remove por uso.")]
        [SerializeField] private float _power = 1f;

        [Header("Desgaste")]
        [Min(0)]
        [Tooltip("Usos até quebrar. 0 = nunca quebra (tablet, engradado).")]
        [SerializeField] private int _maxUses;

        [Tooltip("Quanto custa consertar/repor quando quebra.")]
        [SerializeField] private Money _repairCost;

        [Header("Progressão")]
        [Tooltip("Quando esta ferramenta fica disponível. Vazio = desde o começo.")]
        [SerializeField] private UnlockCondition _unlock;

        [Tooltip("Ordem na roda. Menor aparece primeiro.")]
        [SerializeField] private int _sortOrder;

        public LocalizedText DisplayName => _displayName;
        public Sprite Icon => _icon;
        public GameObject HeldPrefab => _heldPrefab;
        public float Power => _power < 0f ? 0f : _power;
        public int MaxUses => _maxUses < 0 ? 0 : _maxUses;
        public Money RepairCost => _repairCost;
        public UnlockCondition Unlock => _unlock;
        public int SortOrder => _sortOrder;

        /// <summary>Desgasta? Ferramenta sem teto de usos nunca quebra.</summary>
        public bool Wears => MaxUses > 0;

        public bool IsUnlocked(IUnlockContext context) =>
            _unlock == null || _unlock.IsSatisfied(context);

        /// <summary>
        /// Trabalha nesta superfície?
        ///
        /// Superfície nula devolve false, e não true. "Não sei em que superfície
        /// estou" é um asset pela metade, e deixar passar faria qualquer
        /// ferramenta limpar qualquer coisa — o erro mais difícil de notar,
        /// porque o jogo continua funcionando e só o desafio some.
        /// </summary>
        public bool WorksOn(ToolSurface surface)
        {
            if (surface == null || _worksOn == null) return false;

            for (int i = 0; i < _worksOn.Length; i++)
                if (_worksOn[i] == surface) return true;

            return false;
        }

        public string DescribeProblem()
        {
            if (_heldPrefab == null) return "Sem prefab de mão: nada aparece ao selecionar.";
            if (_icon == null) return "Sem ícone: a roda mostra um espaço em branco.";

            if (Wears && _repairCost.IsNegative)
                return "Custo de conserto negativo: quebrar pagaria o jogador.";

            return null;
        }

        public override string ToString() => _displayName.IsEmpty ? name : _displayName.Value;

#if UNITY_EDITOR
        public void EditorInitialize(ToolSurface[] worksOn, int maxUses = 0,
                                     UnlockCondition unlock = null, float power = 1f)
        {
            _worksOn = worksOn;
            _maxUses = maxUses;
            _unlock = unlock;
            _power = power;
        }
#endif
    }
}
