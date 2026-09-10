using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Data
{
    /// <summary>
    /// Uma condição de armazenamento: ambiente, refrigerado, congelado, quente…
    ///
    /// Esta é a peça que quebra o acoplamento entre produto e modelo de móvel.
    /// Hoje o produto diz "eu fico no móvel do tipo Freezer" (<c>allowedFurniture</c>),
    /// então criar um Frigorífico Duplo obriga a revisitar todo produto refrigerado.
    ///
    /// Com o trait, o produto diz "eu preciso de congelado" e o móvel diz "eu
    /// forneço congelado". Um móvel novo é um prefab; nenhum produto é tocado.
    /// </summary>
    [CreateAssetMenu(fileName = "StorageTrait", menuName = "Japan Market/Storage Trait", order = 21)]
    public sealed class StorageTrait : ScriptableObject
    {
        [SerializeField] private LocalizedText _displayName;
        [SerializeField] private Sprite _icon;

        [Tooltip("Móveis que fornecem este trait consomem energia? Alimenta a despesa diária.")]
        [SerializeField] private bool _requiresPower;

        public LocalizedText DisplayName => _displayName;
        public Sprite Icon => _icon;
        public bool RequiresPower => _requiresPower;
    }
}
