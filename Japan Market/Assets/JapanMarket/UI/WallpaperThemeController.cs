using System;
using JapanMarket.Core;
using JapanMarket.Domain;
using UnityEngine;

namespace JapanMarket.UI
{
    /// <summary>Ativa somente o objeto do wallpaper escolhido no desktop.</summary>
    [DisallowMultipleComponent]
    public sealed class WallpaperThemeController : MonoBehaviour
    {
        [Serializable]
        public struct WallpaperOption
        {
            public string name;
            public GameObject wallpaper;
            [Min(0)] public int price;
        }

        private const string PreferenceKey = "JapanMarket.ComputerWallpaper";
        private const string OwnershipPrefix = "JapanMarket.ComputerWallpaper.Owned.";

        [SerializeField] private WallpaperOption[] _wallpapers = Array.Empty<WallpaperOption>();
        [SerializeField, HideInInspector] private int _catalogVersion;

        public int Count => _wallpapers.Length;
        public int SelectedIndex { get; private set; }

        private void OnEnable()
        {
            string saved = PlayerPrefs.GetString(PreferenceKey, string.Empty);
            int index = 0;
            for (int i = 0; i < _wallpapers.Length; i++)
            {
                if (_wallpapers[i].name != saved || !IsOwned(i)) continue;
                index = i;
                break;
            }

            Apply(index);
        }

        public string GetName(int index) => IsValid(index) ? _wallpapers[index].name : string.Empty;
        public Money GetPrice(int index) => Money.FromYen(IsValid(index) ? Mathf.Max(0, _wallpapers[index].price) : 0);
        public bool IsOwned(int index) => IsValid(index) && (_wallpapers[index].price <= 0
            || PlayerPrefs.GetInt(OwnershipPrefix + _wallpapers[index].name, 0) == 1);

        public bool TryPurchase(int index, ILedger ledger)
        {
            if (!IsValid(index) || _wallpapers[index].wallpaper == null) return false;
            if (IsOwned(index)) return true;
            if (ledger == null || !ledger.TryWithdraw(GetPrice(index), TransactionReason.CustomizationCost,
                    "Wallpaper: " + GetName(index))) return false;
            PlayerPrefs.SetInt(OwnershipPrefix + _wallpapers[index].name, 1);
            PlayerPrefs.Save();
            return true;
        }

        public Sprite GetPreview(int index)
        {
            if (!IsValid(index) || _wallpapers[index].wallpaper == null) return null;
            UnityEngine.UI.Image image = _wallpapers[index].wallpaper.GetComponent<UnityEngine.UI.Image>();
            return image != null ? image.sprite : null;
        }

        public void Select(int index)
        {
            if (!IsOwned(index) || _wallpapers[index].wallpaper == null) return;

            Apply(index);
            PlayerPrefs.SetString(PreferenceKey, _wallpapers[index].name);
            PlayerPrefs.Save();
        }

        private bool IsValid(int index) => index >= 0 && index < _wallpapers.Length;

        private void Apply(int selectedIndex)
        {
            SelectedIndex = selectedIndex;
            for (int i = 0; i < _wallpapers.Length; i++)
            {
                GameObject wallpaper = _wallpapers[i].wallpaper;
                if (wallpaper != null) wallpaper.SetActive(i == selectedIndex);
            }
        }
    }
}
