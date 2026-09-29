using System;
using TMPro;
using UnityEngine;

namespace JapanMarket.UI
{
    /// <summary>Controla os botões da tela de customização gravada no prefab do PC.</summary>
    public sealed class CustomizationApp : ComputerApp
    {
        [SerializeField] private WallpaperThemeController _wallpapers;
        [SerializeField] private TextMeshProUGUI _status;
        [SerializeField] private UnityEngine.UI.Button[] _buttons = Array.Empty<UnityEngine.UI.Button>();
        [SerializeField] private TextMeshProUGUI[] _buttonLabels = Array.Empty<TextMeshProUGUI>();

        public override string Title => "Customização";

        private void Awake()
        {
            for (int i = 0; i < _buttons.Length; i++)
            {
                if (_buttons[i] == null) continue;
                int index = i;
                _buttons[i].onClick.AddListener(() => Select(index));
            }
        }

        // Toda a hierarquia visual já existe no prefab.
        protected override void Build() { }

        public override void Refresh()
        {
            if (_wallpapers == null) return;

            if (_status != null)
                _status.text = "Tema atual: " + _wallpapers.GetName(_wallpapers.SelectedIndex);

            for (int i = 0; i < _buttons.Length; i++)
            {
                bool selected = i == _wallpapers.SelectedIndex;
                if (_buttons[i] != null) _buttons[i].interactable = !selected;
                if (i < _buttonLabels.Length && _buttonLabels[i] != null)
                    _buttonLabels[i].text = selected ? "Selecionado" : "Aplicar";
            }
        }

        private void Select(int index)
        {
            if (_wallpapers == null) return;
            _wallpapers.Select(index);
            Refresh();
        }
    }
}
