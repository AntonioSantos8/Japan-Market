using System;
using JapanMarket.Core;
using JapanMarket.Data;
using TMPro;
using UnityEngine;

public sealed class MarketCartRowView : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Image icon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text unitsText;
    [SerializeField] private TMP_Text unitPriceText;
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private TMP_Text totalText;
    [SerializeField] private UnityEngine.UI.Button decreaseButton;
    [SerializeField] private UnityEngine.UI.Button increaseButton;
    [SerializeField] private UnityEngine.UI.Button removeButton;

    private Action decreaseAction;
    private Action increaseAction;
    private Action removeAction;

    private void Awake()
    {
        decreaseButton?.onClick.AddListener(() => decreaseAction?.Invoke());
        increaseButton?.onClick.AddListener(() => increaseAction?.Invoke());
        removeButton?.onClick.AddListener(() => removeAction?.Invoke());
    }

    public void BindProduct(ItemDefinition product, Sprite iconOverride, int boxes,
        Action<ItemDefinition, int> change, Action remove)
    {
        string displayName = !product.DisplayName.IsEmpty ? product.DisplayName.Value : product.name;
        icon.sprite = iconOverride != null ? iconOverride : product.Icon;
        nameText.text = displayName;
        unitsText.text = $"{boxes * product.UnitsPerBox} un ({boxes} cx)";
        unitPriceText.text = product.BoxCost.ToString();
        quantityText.text = boxes.ToString();
        totalText.text = (product.BoxCost * boxes).ToString();
        decreaseAction = () => change(product, -1);
        increaseAction = () => change(product, 1);
        removeAction = remove;
    }

    public void BindFurniture(FurnitureData furniture, int quantity,
        Action<FurnitureData, int> change, Action remove)
    {
        Money price = Money.FromYen(furniture.data != null ? furniture.data.singleItemPrice : 0f);
        icon.sprite = furniture.furnitureImage != null
            ? furniture.furnitureImage
            : furniture.data != null ? furniture.data.itemSprite : null;
        nameText.text = string.IsNullOrWhiteSpace(furniture.furnitureName)
            ? furniture.name
            : furniture.furnitureName;
        unitsText.text = $"{quantity} un";
        unitPriceText.text = price.ToString();
        quantityText.text = quantity.ToString();
        totalText.text = (price * quantity).ToString();
        decreaseAction = () => change(furniture, -1);
        increaseAction = () => change(furniture, 1);
        removeAction = remove;
    }
}
