using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;
using JapanMarket.Domain;
using TMPro;
using UnityEngine;

public sealed class ComputerMarketView : MonoBehaviour
{
    [Header("Tabs")]
    [SerializeField] private UnityEngine.UI.Button productsTabButton;
    [SerializeField] private UnityEngine.UI.Button furnitureTabButton;
    [SerializeField] private UnityEngine.UI.Button cartTabButton;
    [SerializeField] private GameObject productsPage;
    [SerializeField] private GameObject furniturePage;
    [SerializeField] private GameObject cartPage;

    [Header("Catalog grids")]
    [SerializeField] private RectTransform productsGrid;
    [SerializeField] private RectTransform furnitureGrid;
    [SerializeField] private MarketProductCardView productCardPrefab;
    [SerializeField] private MarketFurnitureCardView furnitureCardPrefab;
    [SerializeField] private List<FurnitureData> furnitureCatalog = new();

    [Header("Cart")]
    [SerializeField] private RectTransform cartContent;
    [SerializeField] private MarketCartRowView cartRowPrefab;
    [SerializeField] private TMP_Text subtotalText;
    [SerializeField] private TMP_Text shippingText;
    [SerializeField] private TMP_Text totalText;
    [SerializeField] private TMP_Text balanceText;
    [SerializeField] private TMP_Text cartCountText;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private UnityEngine.UI.Button checkoutButton;
    [SerializeField, Min(0)] private long fixedShippingFeeYen = 800;

    private readonly MarketCart productCart = new();
    private readonly Dictionary<FurnitureData, int> furnitureCart = new();
    private readonly List<ItemDefinition> availableProducts = new();

    private IMarketOrderService market;
    private ILedger ledger;
    private bool built;

    private void Awake()
    {
        productsTabButton?.onClick.AddListener(ShowProducts);
        furnitureTabButton?.onClick.AddListener(ShowFurniture);
        cartTabButton?.onClick.AddListener(ShowCart);
        checkoutButton?.onClick.AddListener(Checkout);
    }

    private void OnEnable()
    {
        ResolveServices();
        if (!built) BuildCatalogs();
        ShowProducts();
        RefreshCart();
    }

    private void OnDestroy()
    {
        productsTabButton?.onClick.RemoveListener(ShowProducts);
        furnitureTabButton?.onClick.RemoveListener(ShowFurniture);
        cartTabButton?.onClick.RemoveListener(ShowCart);
        checkoutButton?.onClick.RemoveListener(Checkout);
    }

    private void ResolveServices()
    {
        ServiceContainer.Current.TryResolve(out market);
        ServiceContainer.Current.TryResolve(out ledger);
    }

    private void BuildCatalogs()
    {
        built = true;
        BuildProducts();
        BuildFurniture();
    }

    private void BuildProducts()
    {
        if (productsGrid == null || productCardPrefab == null || market == null) return;

        market.GetAvailableProducts(availableProducts);
        ItemManager itemManager = ServiceLocator.Get<ItemManager>();

        for (int i = 0; i < availableProducts.Count; i++)
        {
            ItemDefinition product = availableProducts[i];
            AllIThingsData legacy = itemManager != null
                ? itemManager.GetItemData((Items)product.LegacyEnumValue)
                : null;

            MarketProductCardView card = Instantiate(productCardPrefab, productsGrid);
            card.gameObject.SetActive(true);
            card.Bind(legacy, product, AddProductToCart);
        }
    }

    private void BuildFurniture()
    {
        if (furnitureGrid == null || furnitureCardPrefab == null) return;

        for (int i = 0; i < furnitureCatalog.Count; i++)
        {
            FurnitureData furniture = furnitureCatalog[i];
            if (furniture == null) continue;

            MarketFurnitureCardView card = Instantiate(furnitureCardPrefab, furnitureGrid);
            card.gameObject.SetActive(true);
            card.Bind(furniture, AddFurnitureToCart);
        }
    }

    private void AddProductToCart(ItemDefinition product, int boxes)
    {
        productCart.AddBoxes(product, boxes);
        feedbackText.text = string.Empty;
        RefreshCart();
    }

    private void AddFurnitureToCart(FurnitureData furniture, int quantity)
    {
        if (furniture == null || quantity <= 0) return;
        furnitureCart.TryGetValue(furniture, out int current);
        furnitureCart[furniture] = Mathf.Clamp(current + quantity, 1, MarketCart.MaxBoxesPerProduct);
        feedbackText.text = string.Empty;
        RefreshCart();
    }

    private void ChangeProduct(ItemDefinition product, int delta)
    {
        if (delta > 0) productCart.AddBoxes(product, delta);
        else productCart.RemoveBoxes(product, -delta);
        RefreshCart();
    }

    private void ChangeFurniture(FurnitureData furniture, int delta)
    {
        if (!furnitureCart.TryGetValue(furniture, out int current)) return;
        int next = current + delta;
        if (next <= 0) furnitureCart.Remove(furniture);
        else furnitureCart[furniture] = Mathf.Min(next, MarketCart.MaxBoxesPerProduct);
        RefreshCart();
    }

    private void RefreshCart()
    {
        if (cartContent == null || cartRowPrefab == null) return;

        for (int i = cartContent.childCount - 1; i >= 0; i--)
        {
            Transform child = cartContent.GetChild(i);
            if (child.gameObject != cartRowPrefab.gameObject) Destroy(child.gameObject);
        }

        int lineCount = 0;
        ItemManager itemManager = ServiceLocator.Get<ItemManager>();
        foreach (KeyValuePair<ItemDefinition, int> entry in productCart.Items)
        {
            AllIThingsData legacy = itemManager != null
                ? itemManager.GetItemData((Items)entry.Key.LegacyEnumValue)
                : null;
            MarketCartRowView row = Instantiate(cartRowPrefab, cartContent);
            row.gameObject.SetActive(true);
            row.BindProduct(entry.Key, legacy != null ? legacy.itemSprite : null,
                entry.Value, ChangeProduct,
                () => { productCart.SetBoxes(entry.Key, 0); RefreshCart(); });
            lineCount++;
        }

        foreach (KeyValuePair<FurnitureData, int> entry in furnitureCart)
        {
            FurnitureData furniture = entry.Key;
            MarketCartRowView row = Instantiate(cartRowPrefab, cartContent);
            row.gameObject.SetActive(true);
            row.BindFurniture(furniture, entry.Value, ChangeFurniture,
                () => { furnitureCart.Remove(furniture); RefreshCart(); });
            lineCount++;
        }

        Money productSubtotal = ProductSubtotal();
        Money furnitureSubtotal = FurnitureSubtotal();
        Money subtotal = productSubtotal + furnitureSubtotal;
        Money shipping = lineCount > 0 ? Money.FromYen(fixedShippingFeeYen) : Money.Zero;
        Money total = subtotal + shipping;

        subtotalText.text = subtotal.ToString();
        shippingText.text = shipping.ToString();
        totalText.text = total.ToString();
        balanceText.text = ledger != null ? ledger.Balance.ToString() : "—";
        cartCountText.text = lineCount.ToString();
        checkoutButton.interactable = lineCount > 0 && (ledger == null || ledger.CanAfford(total));
    }

    private Money ProductSubtotal()
    {
        Money total = Money.Zero;
        foreach (KeyValuePair<ItemDefinition, int> entry in productCart.Items)
            total += entry.Key.BoxCost * entry.Value;
        return total;
    }

    private Money FurnitureSubtotal()
    {
        Money total = Money.Zero;
        foreach (KeyValuePair<FurnitureData, int> entry in furnitureCart)
            total += Money.FromYen(entry.Key.data != null
                ? entry.Key.data.singleItemPrice * entry.Value
                : 0f);
        return total;
    }

    private void Checkout()
    {
        ResolveServices();
        int itemLines = productCart.Items.Count + furnitureCart.Count;
        if (itemLines == 0) return;

        Money furnitureTotal = FurnitureSubtotal();
        Money shipping = Money.FromYen(fixedShippingFeeYen);
        Money grandTotal = ProductSubtotal() + furnitureTotal + shipping;

        if (ledger == null || !ledger.CanAfford(grandTotal))
        {
            SetFeedback("Saldo insuficiente.", false);
            return;
        }

        FurnitureManager furnitureManager = null;
        if (furnitureCart.Count > 0)
        {
            furnitureManager = ServiceLocator.Get<FurnitureManager>();
            if (furnitureManager == null)
            {
                SetFeedback("Sistema de móveis indisponível.", false);
                return;
            }
        }

        if (productCart.TotalBoxes > 0)
        {
            if (market == null)
            {
                SetFeedback("Fornecedor indisponível.", false);
                return;
            }

            productCart.ShippingFee = shipping;
            productCart.AdditionalCost = furnitureTotal;
            MarketOrderResult result = market.TryCheckout(productCart, out MarketOrder order);
            if (result != MarketOrderResult.Ok)
            {
                SetFeedback(Explain(result), false);
                return;
            }

            DeliverFurniture(furnitureManager);
            SetFeedback($"Pedido #{order.Id} realizado.", true);
        }
        else
        {
            if (!ledger.TryWithdraw(furnitureTotal + shipping,
                    TransactionReason.FurniturePurchase, "Compra pelo computador"))
            {
                SetFeedback("Saldo insuficiente.", false);
                return;
            }

            DeliverFurniture(furnitureManager);
            SetFeedback("Compra realizada.", true);
        }

        productCart.Clear();
        furnitureCart.Clear();
        ServiceLocator.Get<SoundManager>()?.Play(SFX.ComprarItemOuFurnitureComputador);
        RefreshCart();
    }

    private void DeliverFurniture(FurnitureManager manager)
    {
        if (manager == null) return;
        foreach (KeyValuePair<FurnitureData, int> entry in furnitureCart)
            for (int i = 0; i < entry.Value; i++) manager.AddToInventory(entry.Key);
    }

    private void SetFeedback(string message, bool success)
    {
        feedbackText.text = message;
        feedbackText.color = success ? new Color(0.35f, 0.9f, 0.5f) : new Color(1f, 0.4f, 0.4f);
    }

    private static string Explain(MarketOrderResult result) => result switch
    {
        MarketOrderResult.NotEnoughMoney => "Saldo insuficiente.",
        MarketOrderResult.ProductLocked => "Um produto ainda está bloqueado.",
        MarketOrderResult.TooManyPendingOrders => "Há pedidos demais a caminho.",
        MarketOrderResult.Unavailable => "Fornecedor indisponível.",
        _ => "Não foi possível realizar a compra."
    };

    public void ShowProducts() => ShowPage(productsPage);
    public void ShowFurniture() => ShowPage(furniturePage);
    public void ShowCart() => ShowPage(cartPage);

    private void ShowPage(GameObject page)
    {
        if (productsPage != null) productsPage.SetActive(page == productsPage);
        if (furniturePage != null) furniturePage.SetActive(page == furniturePage);
        if (cartPage != null) cartPage.SetActive(page == cartPage);
        if (page == cartPage) RefreshCart();
    }
}
