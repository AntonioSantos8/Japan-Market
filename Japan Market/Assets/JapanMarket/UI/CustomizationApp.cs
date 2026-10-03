using System;
using JapanMarket.Core;
using JapanMarket.Domain;
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
        private ILedger _ledger;
        private IDisposable _balanceSubscription;

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

        protected override void Subscribe()
        {
            Unsubscribe();
            TryGet(out _ledger);
            if (TryGet<IEventBus>(out var events))
                _balanceSubscription = events.Subscribe<BalanceChanged>(_ => Refresh());
        }

        protected override void Unsubscribe()
        {
            _balanceSubscription?.Dispose();
            _balanceSubscription = null;
        }

        public override void Refresh()
        {
            if (_wallpapers == null) return;
            if (_ledger == null) TryGet(out _ledger);

            if (_status != null)
                _status.text = "Tema atual: " + _wallpapers.GetName(_wallpapers.SelectedIndex)
                    + (_ledger != null ? $" • Saldo: {_ledger.Balance}" : string.Empty);

            for (int i = 0; i < _buttons.Length; i++)
            {
                bool selected = i == _wallpapers.SelectedIndex;
                bool owned = _wallpapers.IsOwned(i);
                bool affordable = _ledger != null && _ledger.CanAfford(_wallpapers.GetPrice(i));
                if (_buttons[i] != null)
                    _buttons[i].interactable = i < _wallpapers.Count && !selected && (owned || affordable);
                if (i < _buttonLabels.Length && _buttonLabels[i] != null)
                    _buttonLabels[i].text = selected ? "Selecionado" : owned ? "Aplicar"
                        : _ledger == null ? "Indisponível" : !affordable
                            ? $"{_wallpapers.GetPrice(i)}\nSem saldo" : $"Comprar {_wallpapers.GetPrice(i)}";
            }
        }

        private void Select(int index)
        {
            if (_wallpapers == null) return;
            if (!_wallpapers.IsOwned(index) && !_wallpapers.TryPurchase(index, _ledger))
            {
                Refresh();
                if (_status != null) _status.text = _ledger == null
                    ? "Compras indisponíveis nesta cena." : "Saldo insuficiente para comprar este wallpaper.";
                return;
            }
            _wallpapers.Select(index);
            Refresh();
        }
    }
}
