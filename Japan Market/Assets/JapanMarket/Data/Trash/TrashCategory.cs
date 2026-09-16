using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Data
{
    /// <summary>
    /// Um tipo de lixo reciclável: plástico, metal, papel, orgânico, o que for.
    ///
    /// Asset, e não <c>enum TrashType</c>. O enum obrigava a recompilar para
    /// acrescentar "vidro", e qualquer coisa que guardasse o valor numérico —
    /// um save, um prefab, um asset — passava a apontar para outra categoria se
    /// alguém inserisse um item no meio da lista.
    ///
    /// A <see cref="Key"/> é escrita à mão, e de propósito: ela é o que o save
    /// guarda, e um texto que o designer controla é mais fácil de migrar e de
    /// depurar do que um GUID. O catálogo recusa duas categorias com a mesma
    /// chave, que é o único jeito de isso dar errado.
    /// </summary>
    [CreateAssetMenu(fileName = "TrashCategory",
        menuName = "Japan Market/Trash/Category", order = 70)]
    public sealed class TrashCategory : ScriptableObject
    {
        [Tooltip("Identificador estável, minúsculo e sem espaços: plastico, metal, papel. " +
                 "É o que o save guarda — mudar isto depois perde os sacos pendentes.")]
        [SerializeField] private string _key;

        [SerializeField] private LocalizedText _displayName;

        [Tooltip("Cor da lixeira e do rótulo do saco na interface.")]
        [SerializeField] private Color _color = Color.gray;

        [Tooltip("Bônus pago quando o saco é entregue CHEIO só desta categoria. " +
                 "É o que faz separar o lixo valer a pena.")]
        [SerializeField] private Money _fullBagBonus;

        [Tooltip("Ordem de exibição. Menor aparece primeiro.")]
        [SerializeField] private int _sortOrder;

        public string Key => string.IsNullOrWhiteSpace(_key) ? name : _key.Trim();
        public LocalizedText DisplayName => _displayName;
        public Color Color => _color;
        public Money FullBagBonus => _fullBagBonus;
        public int SortOrder => _sortOrder;

        public bool HasKey => !string.IsNullOrWhiteSpace(_key);

        public override string ToString() =>
            _displayName.IsEmpty ? name : _displayName.Value;

#if UNITY_EDITOR
        public void EditorInitialize(string key, Money fullBagBonus = default)
        {
            _key = key;
            _fullBagBonus = fullBagBonus;
        }
#endif
    }
}
