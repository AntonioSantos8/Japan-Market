using System;
using JapanMarket.Core;
using JapanMarket.Data;
using JapanMarket.Domain;
using TMPro;
using UnityEngine;

public sealed class MarketProductCardView : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Image icon;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text placementText;
    [SerializeField] private TMP_Text unitsPerBoxText;
    [SerializeField] private TMP_Text unitPriceText;
    [SerializeField] private TMP_Text boxPriceText;
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private TMP_Text totalText;
    [SerializeField] private UnityEngine.UI.Button decreaseButton;
    [SerializeField] private UnityEngine.UI.Button increaseButton;
    [SerializeField] private UnityEngine.UI.Button addToCartButton;

    private ItemDefinition product;
    private Action<ItemDefinition, int> addAction;
    private int quantity = 1;

    private void Awake()
    {
        decreaseButton?.onClick.AddListener(Decrease);
        increaseButton?.onClick.AddListener(Increase);
        addToCartButton?.onClick.AddListener(AddToCart);
    }

    public void Bind(AllIThingsData legacy, ItemDefinition definition,
        Action<ItemDefinition, int> onAdd)
    {
        product = definition;
        addAction = onAdd;
        quantity = 1;

        string productName = legacy != null && !string.IsNullOrWhiteSpace(legacy.itemName)
            ? legacy.itemName
            : !definition.DisplayName.IsEmpty ? definition.DisplayName.Value : definition.name;

        titleText.text = productName;
        placementText.text = "Colocável: " + (legacy != null
            ? legacy.allowedFurniture.ToString()
            : definition.RequiredStorage != null ? definition.RequiredStorage.name : "Qualquer");
        unitsPerBoxText.text = $"{definition.UnitsPerBox} por caixa";
        unitPriceText.text = $"Preço por unidade: {Money.FromYen(definition.BoxCost.Yen / (double)definition.UnitsPerBox)}";
        boxPriceText.text = $"Caixa: {definition.BoxCost}";
        icon.sprite = legacy != null && legacy.itemSprite != null ? legacy.itemSprite : definition.Icon;
        icon.preserveAspect = true;
        RefreshQuantity();
    }

    private void Decrease()
    {
        quantity = Mathf.Max(1, quantity - 1);
        RefreshQuantity();
    }

    private void Increase()
    {
        quantity = Mathf.Min(MarketCart.MaxBoxesPerProduct, quantity + 1);
        RefreshQuantity();
    }

    private void AddToCart()
    {
        if (product != null) addAction?.Invoke(product, quantity);
    }

    private void RefreshQuantity()
    {
        quantityText.text = quantity.ToString();
        totalText.text = product != null ? (product.BoxCost * quantity).ToString() : "¥0";
    }
}
