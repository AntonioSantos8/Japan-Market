using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;
using JapanMarket.Domain;
using UnityEngine;

public class GlobalPrices : MonoBehaviour
{
    [SerializeField] private PriceDisplayUI _priceDisplay;
    [SerializeField] private AllIThingsData[] _allItemsData;

    private readonly Dictionary<Items, float> _globalItemsPrice = new();
    private readonly Dictionary<Items, float> _baseItemsPrice = new();
    private readonly Dictionary<Items, float> _discountPercent = new();
    private readonly Dictionary<Items, bool> _hasPutItem = new();

    private Items currentDisplayType = Items.None;
    private IPricingService _pricing;
    private IItemCatalog _catalog;
    private IGameClock _clock;

    private void Awake()
    {
        ServiceLocator.Register(this);
    }

    private void Start()
    {
        foreach (Items item in System.Enum.GetValues(typeof(Items)))
        {
            _globalItemsPrice[item] = 0f;
            _baseItemsPrice[item] = 0f;
            _discountPercent[item] = 0f;
            _hasPutItem[item] = false;
        }

        BindModernPricing();
    }

    private void OnDestroy()
    {
        if (_pricing != null) _pricing.PriceChanged -= OnModernPriceChanged;
    }

    public float GetItemCurrentPrice(Items item)
    {
        return _globalItemsPrice.TryGetValue(item, out float price) ? price : 0f;
    }

    public float GetItemBasePrice(Items item)
    {
        if (_baseItemsPrice.TryGetValue(item, out float price) && price > 0f)
            return price;

        return GetMarketPrice(item);
    }

    public float GetItemDiscount(Items item)
    {
        return _discountPercent.TryGetValue(item, out float discount) ? discount : 0f;
    }

    private void SetPricing(float basePrice, float discount, Items item)
    {
        if (!_globalItemsPrice.ContainsKey(item)) return;

        basePrice = Mathf.Max(0f, Mathf.Round(basePrice));
        discount = Mathf.Clamp(discount, 0f, 100f);

        _baseItemsPrice[item] = basePrice;
        _discountPercent[item] = discount;
        _globalItemsPrice[item] = Mathf.Round(basePrice * (1f - discount / 100f));

        ItemDefinition product = FindProduct(item);
        if (_pricing != null && product != null)
        {
            _pricing.SetSellPrice(product,
                Money.FromYen(_globalItemsPrice[item]), _clock?.Day ?? 0);
        }
    }

    public void Apply()
    {
        if (currentDisplayType == Items.None) return;

        float basePrice = _priceDisplay != null
            ? _priceDisplay.EnteredPrice
            : GetItemBasePrice(currentDisplayType);
        float discount = _priceDisplay != null
            ? _priceDisplay.DiscountPercent
            : GetItemDiscount(currentDisplayType);

        SetPricing(basePrice, discount, currentDisplayType);

        TutorialManager tutorialManager = ServiceLocator.Get<TutorialManager>();
        if (tutorialManager != null)
            tutorialManager.NotifyGameEvent("HasPutPrice");

        SetCurrentDisplayNone();
    }

    public void HasPutItem(Items item)
    {
        if (!_hasPutItem.ContainsKey(item)) return;

        if (!_hasPutItem[item])
        {
            _hasPutItem[item] = true;
            SetPricing(GetMarketPrice(item), 0f, item);
            OpenPriceDisplay(item);
        }
    }

    public void SetCurrentDisplay(Items to)
    {
        currentDisplayType = to;
    }

    public void SetCurrentDisplayNone()
    {
        currentDisplayType = Items.None;
    }

    private AllIThingsData GetItemData(Items item)
    {
        if (_allItemsData == null) return null;

        foreach (AllIThingsData data in _allItemsData)
        {
            if (data != null && data.itemType == item)
                return data;
        }

        return null;
    }

    private void BindModernPricing()
    {
        ServiceContainer.Current.TryResolve(out _pricing);
        ServiceContainer.Current.TryResolve(out _catalog);
        ServiceContainer.Current.TryResolve(out _clock);

        if (_pricing == null || _catalog == null) return;

        _pricing.PriceChanged += OnModernPriceChanged;

        IReadOnlyList<ItemDefinition> products = _catalog.All;
        for (int i = 0; i < products.Count; i++)
        {
            ItemDefinition product = products[i];
            if (product == null || product.LegacyEnumValue < 0) continue;

            Items item = (Items)product.LegacyEnumValue;
            if (!_globalItemsPrice.ContainsKey(item)) continue;

            ApplyModernPrice(item, _pricing.GetSellPrice(product));
        }
    }

    private void OnModernPriceChanged(ItemDefinition product, Money price)
    {
        if (product == null || product.LegacyEnumValue < 0) return;

        Items item = (Items)product.LegacyEnumValue;
        if (_globalItemsPrice.ContainsKey(item)) ApplyModernPrice(item, price);
    }

    private void ApplyModernPrice(Items item, Money price)
    {
        float value = Mathf.Max(0f, (float)price.Yen);
        _baseItemsPrice[item] = value;
        _discountPercent[item] = 0f;
        _globalItemsPrice[item] = value;
    }

    private ItemDefinition FindProduct(Items item)
    {
        if (_catalog == null) return null;

        IReadOnlyList<ItemDefinition> products = _catalog.All;
        for (int i = 0; i < products.Count; i++)
        {
            ItemDefinition product = products[i];
            if (product != null && product.LegacyEnumValue == (int)item) return product;
        }

        return null;
    }

    private float GetMarketPrice(Items item)
    {
        AllIThingsData data = GetItemData(item);
        return data != null ? data.marketPrice : 0f;
    }

    private void OpenPriceDisplay(Items item)
    {
        if (_priceDisplay == null) return;

        AllIThingsData data = GetItemData(item);
        _priceDisplay.ShowDisplay(data, GetItemBasePrice(item), GetItemDiscount(item));
        SetCurrentDisplay(item);
    }

    public void EnablePriceDisplayUI(PriceDisplay display)
    {
        if (display == null) return;
        OpenPriceDisplay(display.GetDisplayItemType);
    }
}
