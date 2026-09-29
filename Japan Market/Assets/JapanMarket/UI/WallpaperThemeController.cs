using System;
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
        }

        private const string PreferenceKey = "JapanMarket.ComputerWallpaper";

        [SerializeField] private WallpaperOption[] _wallpapers = Array.Empty<WallpaperOption>();

        public int Count => _wallpapers.Length;
        public int SelectedIndex { get; private set; }

        private void OnEnable()
        {
            string saved = PlayerPrefs.GetString(PreferenceKey, string.Empty);
            int index = 0;
            for (int i = 0; i < _wallpapers.Length; i++)
            {
                if (_wallpapers[i].name != saved) continue;
                index = i;
                break;
            }

            Apply(index);
        }

        public string GetName(int index) => IsValid(index) ? _wallpapers[index].name : string.Empty;

        public Sprite GetPreview(int index)
        {
            if (!IsValid(index) || _wallpapers[index].wallpaper == null) return null;
            UnityEngine.UI.Image image = _wallpapers[index].wallpaper.GetComponent<UnityEngine.UI.Image>();
            return image != null ? image.sprite : null;
        }

        public void Select(int index)
        {
            if (!IsValid(index) || _wallpapers[index].wallpaper == null) return;

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
