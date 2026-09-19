using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Data
{

    [CreateAssetMenu(fileName = "StorageTrait", menuName = "Japan Market/Storage Trait", order = 21)]
    public sealed class StorageTrait : ScriptableObject
    {
        [SerializeField] private LocalizedText _displayName;
        [SerializeField] private Sprite _icon;

        [Tooltip("Do furniture providing this trait consume energy? Feeds the daily expense.")]
        [SerializeField] private bool _requiresPower;

        public LocalizedText DisplayName => _displayName;
        public Sprite Icon => _icon;
        public bool RequiresPower => _requiresPower;
    }
}
