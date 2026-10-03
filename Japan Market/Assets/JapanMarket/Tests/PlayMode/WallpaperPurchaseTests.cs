using System;
using System.Reflection;
using JapanMarket.Core;
using JapanMarket.Domain;
using JapanMarket.UI;
using NUnit.Framework;
using UnityEngine;

namespace JapanMarket.Tests.PlayMode
{
    public sealed class WallpaperPurchaseTests
    {
        private const string SelectionKey = "JapanMarket.ComputerWallpaper";
        private const string OwnershipPrefix = SelectionKey + ".Owned.";
        private GameObject _root;
        private WallpaperThemeController _themes;
        private CustomizationApp _app;
        private WallpaperThemeController.WallpaperOption[] _options;
        private UnityEngine.UI.Button[] _buttons;
        private Ledger _ledger;
        private object _previousLedger, _previousEvents;
        private bool _hadSelection;
        private string _previousSelection;

        [SetUp]
        public void SetUp()
        {
            _hadSelection = PlayerPrefs.HasKey(SelectionKey);
            _previousSelection = PlayerPrefs.GetString(SelectionKey);
            PlayerPrefs.DeleteKey(SelectionKey);
            var events = new EventBus();
            _ledger = new Ledger(events, null, Money.FromYen(1000));
            ServiceContainer.Current.TryResolveAs(typeof(ILedger), out _previousLedger);
            ServiceContainer.Current.TryResolveAs(typeof(IEventBus), out _previousEvents);
            ServiceContainer.Current.RegisterAs(typeof(ILedger), _ledger);
            ServiceContainer.Current.RegisterAs(typeof(IEventBus), events);
            _root = new GameObject("Wallpaper purchase regression");
            _root.SetActive(false);
            _themes = _root.AddComponent<WallpaperThemeController>();
            _app = _root.AddComponent<CustomizationApp>();
            _options = new WallpaperThemeController.WallpaperOption[3];
            _buttons = new UnityEngine.UI.Button[3];
            for (int i = 0; i < 3; i++)
            {
                var wallpaper = new GameObject("Test wallpaper " + i);
                wallpaper.transform.SetParent(_root.transform, false);
                _options[i] = new WallpaperThemeController.WallpaperOption
                {
                    name = "PurchaseTest-" + Guid.NewGuid().ToString("N"),
                    wallpaper = wallpaper,
                    price = i * 500
                };
                var button = new GameObject("Test button " + i, typeof(RectTransform));
                button.transform.SetParent(_root.transform, false);
                _buttons[i] = button.AddComponent<UnityEngine.UI.Button>();
            }
            Set(_themes, "_wallpapers", _options);
            Set(_app, "_wallpapers", _themes);
            Set(_app, "_buttons", _buttons);
            _root.SetActive(true);
            _app.Open();
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        [TearDown]
        public void TearDown()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
            _ledger?.Dispose();
            RestoreService(typeof(ILedger), _previousLedger);
            RestoreService(typeof(IEventBus), _previousEvents);
            if (_options != null)
                foreach (var option in _options) PlayerPrefs.DeleteKey(OwnershipPrefix + option.name);
            if (_hadSelection) PlayerPrefs.SetString(SelectionKey, _previousSelection);
            else PlayerPrefs.DeleteKey(SelectionKey);
            PlayerPrefs.Save();
        }

        private static void RestoreService(Type type, object previous)
        {
            if (previous == null) ServiceContainer.Current.UnregisterAs(type);
            else ServiceContainer.Current.RegisterAs(type, previous);
        }

        [Test]
        public void Purchase_button_charges_once_unlocks_applies_and_restores_the_theme()
        {
            _buttons[1].onClick.Invoke();
            Assert.That(_ledger.Balance, Is.EqualTo(Money.FromYen(500)));
            Assert.That(_ledger.Today.Count, Is.EqualTo(1));
            Assert.That(_ledger.Today[0].Reason, Is.EqualTo(TransactionReason.CustomizationCost));
            Assert.That(_themes.IsOwned(1), Is.True);
            Assert.That(_themes.SelectedIndex, Is.EqualTo(1));
            Assert.That(_options[1].wallpaper.activeSelf, Is.True);
            Assert.That(_options[0].wallpaper.activeSelf, Is.False);
            _themes.enabled = false;
            _themes.enabled = true;
            Assert.That(_themes.SelectedIndex, Is.EqualTo(1));
            Assert.That(_themes.TryPurchase(1, _ledger), Is.True);
            Assert.That(_ledger.Today.Count, Is.EqualTo(1));
            Assert.That(_ledger.Balance, Is.EqualTo(Money.FromYen(500)));
        }

        [Test]
        public void Insufficient_balance_does_not_unlock_charge_or_change_the_wallpaper()
        {
            _ledger.TryWithdraw(Money.FromYen(501), TransactionReason.StockPurchase);
            Assert.That(_buttons[1].interactable, Is.False);
            Assert.That(_themes.TryPurchase(1, _ledger), Is.False);
            _themes.Select(1);
            Assert.That(_themes.SelectedIndex, Is.Zero);
            Assert.That(_themes.IsOwned(1), Is.False);
            Assert.That(_ledger.Balance, Is.EqualTo(Money.FromYen(499)));
            Assert.That(_ledger.Today.Count, Is.EqualTo(1));
        }

        [Test]
        public void Free_themes_work_without_payment_and_balance_changes_refresh_purchase_buttons()
        {
            Assert.That(_themes.TryPurchase(0, null), Is.True);
            _buttons[1].onClick.Invoke();
            Assert.That(_buttons[2].interactable, Is.False);
            _ledger.Deposit(Money.FromYen(500), TransactionReason.ProductSale);
            Assert.That(_buttons[2].interactable, Is.True);
            Assert.That(_buttons[1].interactable, Is.False);
        }

        [Test]
        public void Missing_payment_service_and_invalid_options_cannot_unlock_paid_themes()
        {
            Assert.That(_themes.TryPurchase(1, null), Is.False);
            Assert.That(_themes.TryPurchase(-1, _ledger), Is.False);
            Assert.That(_themes.TryPurchase(99, _ledger), Is.False);
            Assert.That(_themes.IsOwned(1), Is.False);
            Assert.That(_ledger.Today, Is.Empty);
        }
    }
}
