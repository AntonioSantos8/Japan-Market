using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Data
{
    /// <summary>
    /// Categoria de produto — bebidas, congelados, massas, doces…
    ///
    /// É um asset, não um enum, pela mesma razão que o produto é: categoria é
    /// conteúdo. Adicionar "Higiene" ao jogo tem que ser criar um arquivo, não
    /// recompilar o projeto e torcer para nenhum índice ter deslocado.
    /// </summary>
    [CreateAssetMenu(fileName = "Category", menuName = "Japan Market/Product Category", order = 20)]
    public sealed class ProductCategory : ScriptableObject
    {
        [SerializeField] private LocalizedText _displayName;
        [SerializeField] private Sprite _icon;
        [SerializeField] private Color _tint = Color.white;

        [Tooltip("Ordem de exibição no catálogo do computador. Menor aparece antes.")]
        [SerializeField] private int _sortOrder;

        public LocalizedText DisplayName => _displayName;
        public Sprite Icon => _icon;
        public Color Tint => _tint;
        public int SortOrder => _sortOrder;
    }
}
