using System;
using JapanMarket.Core;
using JapanMarket.Domain;
using TMPro;
using UnityEngine;

public sealed class MarketFurnitureCardView : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Image icon;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text typeText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private TMP_Text totalText;
    [SerializeField] private UnityEngine.UI.Button decreaseButton;
    [SerializeField] private UnityEngine.UI.Button increaseButton;
    [SerializeField] private UnityEngine.UI.Button addToCartButton;

    private FurnitureData furniture;
    private Action<FurnitureData, int> addAction;
    private int quantity = 1;
    private int maxQuantity = MarketCart.MaxBoxesPerProduct;

    private void Awake()
    {
        decreaseButton?.onClick.AddListener(Decrease);
        increaseButton?.onClick.AddListener(Increase);
        addToCartButton?.onClick.AddListener(AddToCart);
    }

    public void Bind(FurnitureData data, Action<FurnitureData, int> onAdd,
        int quantityLimit = MarketCart.MaxBoxesPerProduct)
    {
        furniture = data;
        addAction = onAdd;
        quantity = 1;
        maxQuantity = Mathf.Max(1, quantityLimit);

        titleText.text = string.IsNullOrWhiteSpace(data.furnitureName) ? data.name : data.furnitureName;
        typeText.text = "Tipo: " + data.type;
        priceText.text = "Preço: " + UnitPrice;
        icon.sprite = data.furnitureImage != null
            ? data.furnitureImage
            : data.data != null ? data.data.itemSprite : null;
        icon.preserveAspect = true;
        RefreshQuantity();
    }

    private Money UnitPrice => Money.FromYen(furniture != null && furniture.data != null
        ? furniture.data.singleItemPrice
        : 0f);

    private void Decrease() { quantity = Mathf.Max(1, quantity - 1); RefreshQuantity(); }
    private void Increase() { quantity = Mathf.Min(maxQuantity, quantity + 1); RefreshQuantity(); }
    private void AddToCart() { if (furniture != null) addAction?.Invoke(furniture, quantity); }

    private void RefreshQuantity()
    {
        quantityText.text = quantity.ToString();
        totalText.text = (UnitPrice * quantity).ToString();
    }
}
