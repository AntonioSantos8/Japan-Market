using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Data
{
    /// <summary>
    /// Um tipo de lixo que aparece no chão da loja: garrafa, lata, embalagem.
    ///
    /// Substitui o <c>TrashData</c> legado, que tinha três campos e um enum. O
    /// que muda de verdade: a categoria é um asset e o valor é <see cref="Money"/>,
    /// então o lixo participa do livro-razão como qualquer outra receita em vez
    /// de somar um <c>float</c> solto na lixeira.
    /// </summary>
    [CreateAssetMenu(fileName = "Trash", menuName = "Japan Market/Trash/Item", order = 71)]
    public sealed class TrashDefinition : ScriptableObject
    {
        [Tooltip("Identificador estável, minúsculo e sem espaços: garrafa-pet, lata. " +
                 "É o que o save guarda para reconstruir um saco pela metade.")]
        [SerializeField] private string _key;

        [SerializeField] private LocalizedText _displayName;

        [Tooltip("A que reciclagem este lixo pertence. Sem categoria ele não é reciclável.")]
        [SerializeField] private TrashCategory _category;

        [Tooltip("O que aparece no chão. Precisa ter um TrashItem.")]
        [SerializeField] private GameObject _prefab;

        [Tooltip("Quanto este item vale quando o caminhão recolhe o saco.")]
        [SerializeField] private Money _value;

        [Min(0f)]
        [Tooltip("Peso relativo no sorteio de que lixo aparece. 0 nunca aparece.")]
        [SerializeField] private float _spawnWeight = 1f;

        /// <summary>
        /// Cai para o nome do asset quando vazio, para o save nunca ficar sem
        /// chave nenhuma — o validador reclama de quem não preencheu, e é lá que
        /// o problema deve doer, não no carregamento do jogador.
        /// </summary>
        public string Key => string.IsNullOrWhiteSpace(_key) ? name : _key.Trim();
        public bool HasKey => !string.IsNullOrWhiteSpace(_key);

        public LocalizedText DisplayName => _displayName;
        public TrashCategory Category => _category;
        public GameObject Prefab => _prefab;
        public Money Value => _value;
        public float SpawnWeight => _spawnWeight < 0f ? 0f : _spawnWeight;

        /// <summary>
        /// Utilizável pelas REGRAS. Só a categoria entra aqui: é ela que decide
        /// em que saco o lixo cabe, e é o único campo sem o qual o Domain não
        /// consegue fazer nada.
        ///
        /// O prefab faltando é problema de catálogo, não de regra — reportado
        /// pelo <see cref="DescribeProblem"/> e checado pelo spawner. Exigi-lo
        /// aqui tornaria toda a lógica de saco intestável sem um prefab de
        /// mentira no projeto.
        /// </summary>
        public bool IsValid => _category != null;

        public string DescribeProblem()
        {
            if (_category == null) return "Sem categoria: não entra em nenhum saco.";
            if (_prefab == null) return "Sem prefab: não tem como aparecer no chão.";
            if (_value.IsNegative) return "Valor negativo: reciclar custaria dinheiro.";

            return null;
        }

        public override string ToString() =>
            _displayName.IsEmpty ? name : _displayName.Value;

#if UNITY_EDITOR
        public void EditorInitialize(TrashCategory category, Money value,
                                     GameObject prefab = null, string key = null)
        {
            _category = category;
            _value = value;
            _prefab = prefab;
            _key = key;
        }
#endif
    }
}
