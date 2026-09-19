using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class ComputerMarketUIPrefabBaker
{
    private const string ComputerPrefab = "Assets/Prefabs/OBJECTS/Furnitures/Computer.prefab";
    private const string UiFolder = "Assets/Prefabs/UI/ComputerMarket";
    private const string ProductCardPath = UiFolder + "/ProductCard.prefab";
    private const string FurnitureCardPath = UiFolder + "/FurnitureCard.prefab";
    private const string CartRowPath = UiFolder + "/CartRow.prefab";

    private static readonly Color Navy = new(0.025f, 0.16f, 0.24f, 1f);
    private static readonly Color Blue = new(0.03f, 0.48f, 0.82f, 1f);
    private static readonly Color Cyan = new(0.1f, 0.65f, 0.88f, 1f);
    private static readonly Color Panel = new(0.04f, 0.24f, 0.34f, 1f);
    private static readonly Color Light = new(0.91f, 0.95f, 0.97f, 1f);
    private static readonly Color White = new(0.97f, 0.98f, 1f, 1f);
    private static readonly Color Green = new(0.05f, 0.78f, 0.35f, 1f);
    private static readonly Color Red = new(0.95f, 0.25f, 0.32f, 1f);

    static ComputerMarketUIPrefabBaker()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += BakeWhenReady;
    }

    [MenuItem("Tools/Japan Market/Computer/Gravar Mercado no Prefab")]
    private static void BakeFromMenu() => Bake(true);

    private static void BakeWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        Bake(false);
    }

    private static void Bake(bool logSuccess)
    {
        EnsureFolder();
        MarketProductCardView productPrefab = EnsureProductCardPrefab();
        MarketFurnitureCardView furniturePrefab = EnsureFurnitureCardPrefab();
        MarketCartRowView cartRowPrefab = EnsureCartRowPrefab();

        GameObject root = PrefabUtility.LoadPrefabContents(ComputerPrefab);
        try
        {
            Transform marketApp = FindDeep(root.transform, "App - Mercado");
            if (marketApp == null)
            {
                if (logSuccess)
                    Debug.LogWarning("[Computer Market] A UI base ainda não foi gravada. " +
                                     "Use primeiro Tools/Japan Market/Computer/Gravar UI no Prefab.");
                return;
            }

            Transform content = marketApp.Find("Window/Content - Edit Here");
            if (content == null)
            {
                Debug.LogError("[Computer Market] Content - Edit Here não encontrado no app Mercado.");
                return;
            }

            if (content.Find("Market Store") != null) return;

            for (int i = 0; i < content.childCount; i++)
                content.GetChild(i).gameObject.SetActive(false);

            RectTransform store = Rect("Market Store", content);
            Stretch(store);
            ComputerMarketView controller = store.gameObject.AddComponent<ComputerMarketView>();

            RectTransform tabs = Rect("Tabs", store);
            tabs.anchorMin = new Vector2(0f, 1f);
            tabs.anchorMax = Vector2.one;
            tabs.pivot = new Vector2(0.5f, 1f);
            tabs.offsetMin = new Vector2(0f, -70f);
            tabs.offsetMax = Vector2.zero;
            var tabsLayout = tabs.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            tabsLayout.spacing = 10f;
            tabsLayout.padding = new RectOffset(8, 8, 8, 8);
            tabsLayout.childForceExpandWidth = true;
            tabsLayout.childForceExpandHeight = true;

            UnityEngine.UI.Button productsTab = Button("Products Tab", tabs, "PRODUTOS", Blue);
            UnityEngine.UI.Button furnitureTab = Button("Furniture Tab", tabs, "MÓVEIS", Cyan);
            UnityEngine.UI.Button cartTab = Button("Cart Tab", tabs, "CARRINHO", Green);

            RectTransform pages = Rect("Pages", store);
            pages.anchorMin = Vector2.zero;
            pages.anchorMax = Vector2.one;
            pages.offsetMin = Vector2.zero;
            pages.offsetMax = new Vector2(0f, -78f);

            RectTransform productsPage = CreateCatalogPage("Products Page", pages,
                new Vector2(820f, 270f), 2, out RectTransform productsGrid);
            RectTransform furniturePage = CreateCatalogPage("Furniture Page", pages,
                new Vector2(520f, 240f), 3, out RectTransform furnitureGrid);
            RectTransform cartPage = CreateCartPage(pages, out RectTransform cartContent,
                out TMP_Text subtotal, out TMP_Text shipping, out TMP_Text total,
                out TMP_Text balance, out TMP_Text count, out TMP_Text feedback,
                out UnityEngine.UI.Button checkout);

            furniturePage.gameObject.SetActive(false);
            cartPage.gameObject.SetActive(false);

            SerializedObject so = new(controller);
            Set(so, "productsTabButton", productsTab);
            Set(so, "furnitureTabButton", furnitureTab);
            Set(so, "cartTabButton", cartTab);
            Set(so, "productsPage", productsPage.gameObject);
            Set(so, "furniturePage", furniturePage.gameObject);
            Set(so, "cartPage", cartPage.gameObject);
            Set(so, "productsGrid", productsGrid);
            Set(so, "furnitureGrid", furnitureGrid);
            Set(so, "productCardPrefab", productPrefab);
            Set(so, "furnitureCardPrefab", furniturePrefab);
            Set(so, "cartContent", cartContent);
            Set(so, "cartRowPrefab", cartRowPrefab);
            Set(so, "subtotalText", subtotal);
            Set(so, "shippingText", shipping);
            Set(so, "totalText", total);
            Set(so, "balanceText", balance);
            Set(so, "cartCountText", count);
            Set(so, "feedbackText", feedback);
            Set(so, "checkoutButton", checkout);
            so.FindProperty("fixedShippingFeeYen").longValue = 800;
            FillFurnitureCatalog(so.FindProperty("furnitureCatalog"));
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, ComputerPrefab);
            AssetDatabase.SaveAssets();
            if (logSuccess) Debug.Log("[Computer Market] Mercado gravado no prefab do computador.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static RectTransform CreateCatalogPage(string name, Transform parent,
        Vector2 cellSize, int columns, out RectTransform content)
    {
        RectTransform page = Rect(name, parent);
        Stretch(page);
        UnityEngine.UI.Image background = page.gameObject.AddComponent<UnityEngine.UI.Image>();
        background.color = new Color(0.02f, 0.09f, 0.13f, 0.96f);

        RectTransform scrollRoot = Rect("Scroll View", page);
        Stretch(scrollRoot, 12f);
        UnityEngine.UI.ScrollRect scroll = scrollRoot.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 35f;

        UnityEngine.UI.Image viewportImage = Image("Viewport", scrollRoot,
            new Color(0f, 0f, 0f, 0.08f));
        Stretch(viewportImage.rectTransform);
        UnityEngine.UI.Mask mask = viewportImage.gameObject.AddComponent<UnityEngine.UI.Mask>();
        mask.showMaskGraphic = false;

        content = Rect("Content Grid", viewportImage.transform);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;

        UnityEngine.UI.GridLayoutGroup grid = content.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
        grid.cellSize = cellSize;
        grid.spacing = new Vector2(18f, 18f);
        grid.padding = new RectOffset(18, 18, 18, 18);
        grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.childAlignment = TextAnchor.UpperLeft;

        UnityEngine.UI.ContentSizeFitter fitter = content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
        fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewportImage.rectTransform;
        scroll.content = content;
        return page;
    }

    private static RectTransform CreateCartPage(Transform parent, out RectTransform content,
        out TMP_Text subtotal, out TMP_Text shipping, out TMP_Text total, out TMP_Text balance,
        out TMP_Text count, out TMP_Text feedback, out UnityEngine.UI.Button checkout)
    {
        RectTransform page = Rect("Cart Page", parent);
        Stretch(page);
        Image("Background", page, Light).raycastTarget = false;

        RectTransform listPanel = Rect("Items Panel", page);
        listPanel.anchorMin = Vector2.zero;
        listPanel.anchorMax = new Vector2(0.72f, 1f);
        listPanel.offsetMin = new Vector2(16f, 16f);
        listPanel.offsetMax = new Vector2(-8f, -16f);

        RectTransform scrollRoot = Rect("Scroll View", listPanel);
        Stretch(scrollRoot);
        UnityEngine.UI.ScrollRect scroll = scrollRoot.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;

        UnityEngine.UI.Image viewport = Image("Viewport", scrollRoot, Color.white);
        Stretch(viewport.rectTransform);
        viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;

        content = Rect("Cart Content", viewport.transform);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;
        UnityEngine.UI.VerticalLayoutGroup layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit =
            UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport.rectTransform;
        scroll.content = content;

        UnityEngine.UI.Image summary = Image("Order Summary", page, Cyan);
        summary.rectTransform.anchorMin = new Vector2(0.72f, 0f);
        summary.rectTransform.anchorMax = Vector2.one;
        summary.rectTransform.offsetMin = new Vector2(8f, 16f);
        summary.rectTransform.offsetMax = new Vector2(-16f, -16f);

        TMP_Text heading = Text("Heading", summary.transform, "RESUMO", 34f,
            TextAlignmentOptions.Center, White);
        Anchor(heading.rectTransform, 0.05f, 0.88f, 0.95f, 0.98f);
        count = SummaryLine(summary.transform, "Itens", 0.77f, out _);
        subtotal = SummaryLine(summary.transform, "Subtotal", 0.66f, out _);
        shipping = SummaryLine(summary.transform, "Frete", 0.55f, out _);
        total = SummaryLine(summary.transform, "TOTAL", 0.40f, out TMP_Text totalLabel);
        totalLabel.fontSize = 30f;
        total.fontSize = 32f;
        balance = SummaryLine(summary.transform, "Saldo", 0.25f, out _);
        feedback = Text("Feedback", summary.transform, string.Empty, 19f,
            TextAlignmentOptions.Center, White);
        Anchor(feedback.rectTransform, 0.06f, 0.13f, 0.94f, 0.22f);
        checkout = Button("Checkout Button", summary.transform, "COMPRAR", Green);
        Anchor((RectTransform)checkout.transform, 0.18f, 0.03f, 0.82f, 0.12f);
        return page;
    }

    private static TMP_Text SummaryLine(Transform parent, string label, float y,
        out TMP_Text labelText)
    {
        labelText = Text(label + " Label", parent, label, 22f,
            TextAlignmentOptions.MidlineLeft, White);
        Anchor(labelText.rectTransform, 0.07f, y, 0.48f, y + 0.08f);
        TMP_Text value = Text(label + " Value", parent, "¥0", 22f,
            TextAlignmentOptions.MidlineRight, White);
        Anchor(value.rectTransform, 0.48f, y, 0.93f, y + 0.08f);
        return value;
    }

    private static MarketProductCardView EnsureProductCardPrefab()
    {
        MarketProductCardView existing = AssetDatabase.LoadAssetAtPath<MarketProductCardView>(ProductCardPath);
        if (existing != null) return existing;

        RectTransform root = CardRoot("Product Card", new Vector2(820f, 270f));
        MarketProductCardView view = root.gameObject.AddComponent<MarketProductCardView>();
        UnityEngine.UI.Image icon = Image("Icon", root, Color.white);
        Anchor(icon.rectTransform, 0.02f, 0.27f, 0.23f, 0.92f);
        TMP_Text title = Text("Title", root, "Produto", 28f, TextAlignmentOptions.TopLeft, White);
        Anchor(title.rectTransform, 0.26f, 0.73f, 0.68f, 0.93f);
        TMP_Text placement = Text("Placement", root, "Colocável: Shelf", 18f, TextAlignmentOptions.Left, White);
        Anchor(placement.rectTransform, 0.26f, 0.57f, 0.68f, 0.72f);
        TMP_Text units = Text("Units Per Box", root, "8 por caixa", 18f, TextAlignmentOptions.Left, White);
        Anchor(units.rectTransform, 0.26f, 0.43f, 0.68f, 0.57f);
        TMP_Text unitPrice = Text("Unit Price", root, "Preço por unidade: ¥0", 16f, TextAlignmentOptions.Left, White);
        Anchor(unitPrice.rectTransform, 0.26f, 0.29f, 0.68f, 0.43f);
        TMP_Text boxPrice = Text("Box Price", root, "Caixa: ¥0", 20f, TextAlignmentOptions.Left, White);
        Anchor(boxPrice.rectTransform, 0.26f, 0.12f, 0.68f, 0.29f);
        UnityEngine.UI.Button minus = Button("Minus", root, "−", Cyan);
        Anchor((RectTransform)minus.transform, 0.72f, 0.65f, 0.80f, 0.86f);
        TMP_Text quantity = Text("Quantity", root, "1", 26f, TextAlignmentOptions.Center, White);
        Anchor(quantity.rectTransform, 0.80f, 0.65f, 0.88f, 0.86f);
        UnityEngine.UI.Button plus = Button("Plus", root, "+", Cyan);
        Anchor((RectTransform)plus.transform, 0.88f, 0.65f, 0.96f, 0.86f);
        TMP_Text total = Text("Total", root, "¥0", 26f, TextAlignmentOptions.Center, White);
        Anchor(total.rectTransform, 0.72f, 0.42f, 0.96f, 0.62f);
        UnityEngine.UI.Button add = Button("Add To Cart", root, "ADICIONAR AO CARRINHO", Green);
        Anchor((RectTransform)add.transform, 0.70f, 0.10f, 0.97f, 0.36f);
        AssignProductCard(view, icon, title, placement, units, unitPrice, boxPrice, quantity, total, minus, plus, add);
        return SavePrefab(root.gameObject, ProductCardPath).GetComponent<MarketProductCardView>();
    }

    private static MarketFurnitureCardView EnsureFurnitureCardPrefab()
    {
        MarketFurnitureCardView existing = AssetDatabase.LoadAssetAtPath<MarketFurnitureCardView>(FurnitureCardPath);
        if (existing != null) return existing;
        RectTransform root = CardRoot("Furniture Card", new Vector2(520f, 240f));
        MarketFurnitureCardView view = root.gameObject.AddComponent<MarketFurnitureCardView>();
        UnityEngine.UI.Image icon = Image("Icon", root, Color.white);
        Anchor(icon.rectTransform, 0.03f, 0.28f, 0.30f, 0.90f);
        TMP_Text title = Text("Title", root, "Móvel", 27f, TextAlignmentOptions.TopLeft, White);
        Anchor(title.rectTransform, 0.34f, 0.70f, 0.95f, 0.92f);
        TMP_Text type = Text("Type", root, "Tipo", 18f, TextAlignmentOptions.Left, White);
        Anchor(type.rectTransform, 0.34f, 0.54f, 0.95f, 0.70f);
        TMP_Text price = Text("Price", root, "Preço: ¥0", 20f, TextAlignmentOptions.Left, White);
        Anchor(price.rectTransform, 0.34f, 0.38f, 0.95f, 0.54f);
        UnityEngine.UI.Button minus = Button("Minus", root, "−", Cyan);
        Anchor((RectTransform)minus.transform, 0.34f, 0.17f, 0.46f, 0.36f);
        TMP_Text quantity = Text("Quantity", root, "1", 23f, TextAlignmentOptions.Center, White);
        Anchor(quantity.rectTransform, 0.46f, 0.17f, 0.57f, 0.36f);
        UnityEngine.UI.Button plus = Button("Plus", root, "+", Cyan);
        Anchor((RectTransform)plus.transform, 0.57f, 0.17f, 0.69f, 0.36f);
        TMP_Text total = Text("Total", root, "¥0", 21f, TextAlignmentOptions.Center, White);
        Anchor(total.rectTransform, 0.70f, 0.17f, 0.96f, 0.36f);
        UnityEngine.UI.Button add = Button("Add To Cart", root, "CARRINHO", Green);
        Anchor((RectTransform)add.transform, 0.04f, 0.04f, 0.30f, 0.22f);
        SerializedObject so = new(view);
        Set(so, "icon", icon); Set(so, "titleText", title); Set(so, "typeText", type);
        Set(so, "priceText", price); Set(so, "quantityText", quantity); Set(so, "totalText", total);
        Set(so, "decreaseButton", minus); Set(so, "increaseButton", plus); Set(so, "addToCartButton", add);
        so.ApplyModifiedPropertiesWithoutUndo();
        return SavePrefab(root.gameObject, FurnitureCardPath).GetComponent<MarketFurnitureCardView>();
    }

    private static MarketCartRowView EnsureCartRowPrefab()
    {
        MarketCartRowView existing = AssetDatabase.LoadAssetAtPath<MarketCartRowView>(CartRowPath);
        if (existing != null) return existing;
        RectTransform root = CardRoot("Cart Row", new Vector2(1200f, 82f));
        root.GetComponent<UnityEngine.UI.Image>().color = new Color(1f, 1f, 1f, 0.96f);
        root.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 82f;
        MarketCartRowView view = root.gameObject.AddComponent<MarketCartRowView>();
        UnityEngine.UI.Image icon = Image("Icon", root, Color.white); Anchor(icon.rectTransform, 0.01f, 0.12f, 0.075f, 0.88f);
        TMP_Text name = Text("Name", root, "Produto", 19f, TextAlignmentOptions.Left, Navy); Anchor(name.rectTransform, 0.085f, 0f, 0.32f, 1f);
        TMP_Text units = Text("Units", root, "8 un", 17f, TextAlignmentOptions.Center, Navy); Anchor(units.rectTransform, 0.32f, 0f, 0.45f, 1f);
        TMP_Text price = Text("Unit Price", root, "¥0", 17f, TextAlignmentOptions.Center, Navy); Anchor(price.rectTransform, 0.45f, 0f, 0.57f, 1f);
        UnityEngine.UI.Button minus = Button("Minus", root, "−", Cyan); Anchor((RectTransform)minus.transform, 0.59f, 0.18f, 0.64f, 0.82f);
        TMP_Text quantity = Text("Quantity", root, "1", 20f, TextAlignmentOptions.Center, Navy); Anchor(quantity.rectTransform, 0.64f, 0f, 0.70f, 1f);
        UnityEngine.UI.Button plus = Button("Plus", root, "+", Cyan); Anchor((RectTransform)plus.transform, 0.70f, 0.18f, 0.75f, 0.82f);
        TMP_Text total = Text("Total", root, "¥0", 19f, TextAlignmentOptions.Center, Navy); Anchor(total.rectTransform, 0.76f, 0f, 0.91f, 1f);
        UnityEngine.UI.Button remove = Button("Remove", root, "REMOVER", Red); Anchor((RectTransform)remove.transform, 0.91f, 0.18f, 0.995f, 0.82f);
        SerializedObject so = new(view);
        Set(so, "icon", icon); Set(so, "nameText", name); Set(so, "unitsText", units);
        Set(so, "unitPriceText", price); Set(so, "quantityText", quantity); Set(so, "totalText", total);
        Set(so, "decreaseButton", minus); Set(so, "increaseButton", plus); Set(so, "removeButton", remove);
        so.ApplyModifiedPropertiesWithoutUndo();
        return SavePrefab(root.gameObject, CartRowPath).GetComponent<MarketCartRowView>();
    }

    private static void AssignProductCard(MarketProductCardView view, UnityEngine.UI.Image icon,
        TMP_Text title, TMP_Text placement, TMP_Text units, TMP_Text unitPrice, TMP_Text boxPrice,
        TMP_Text quantity, TMP_Text total, UnityEngine.UI.Button minus,
        UnityEngine.UI.Button plus, UnityEngine.UI.Button add)
    {
        SerializedObject so = new(view);
        Set(so, "icon", icon); Set(so, "titleText", title); Set(so, "placementText", placement);
        Set(so, "unitsPerBoxText", units); Set(so, "unitPriceText", unitPrice); Set(so, "boxPriceText", boxPrice);
        Set(so, "quantityText", quantity); Set(so, "totalText", total); Set(so, "decreaseButton", minus);
        Set(so, "increaseButton", plus); Set(so, "addToCartButton", add);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static RectTransform CardRoot(string name, Vector2 size)
    {
        RectTransform root = Rect(name, null);
        root.sizeDelta = size;
        Image("Border", root, Navy).rectTransform.SetAsFirstSibling();
        Stretch((RectTransform)root.GetChild(0));
        UnityEngine.UI.Image background = root.gameObject.AddComponent<UnityEngine.UI.Image>();
        background.color = Panel;
        return root;
    }

    private static GameObject SavePrefab(GameObject temporary, string path)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temporary, path);
        Object.DestroyImmediate(temporary);
        return prefab;
    }

    private static void FillFurnitureCatalog(SerializedProperty property)
    {
        string[] guids = AssetDatabase.FindAssets("t:FurnitureData");
        var items = new List<FurnitureData>();
        for (int i = 0; i < guids.Length; i++)
        {
            FurnitureData item = AssetDatabase.LoadAssetAtPath<FurnitureData>(
                AssetDatabase.GUIDToAssetPath(guids[i]));
            if (item != null && item.data != null) items.Add(item);
        }
        property.arraySize = items.Count;
        for (int i = 0; i < items.Count; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI")) AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
        if (!AssetDatabase.IsValidFolder(UiFolder)) AssetDatabase.CreateFolder("Assets/Prefabs/UI", "ComputerMarket");
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = (RectTransform)go.transform;
        if (parent != null) rect.SetParent(parent, false);
        return rect;
    }

    private static UnityEngine.UI.Image Image(string name, Transform parent, Color color)
    {
        RectTransform rect = Rect(name, parent);
        UnityEngine.UI.Image image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = color;
        return image;
    }

    private static UnityEngine.UI.Button Button(string name, Transform parent, string label, Color color)
    {
        UnityEngine.UI.Image image = Image(name, parent, color);
        UnityEngine.UI.Button button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        TMP_Text text = Text("Label", image.transform, label, 20f, TextAlignmentOptions.Center, White);
        Stretch(text.rectTransform, 4f);
        return button;
    }

    private static TMP_Text Text(string name, Transform parent, string value, float size,
        TextAlignmentOptions alignment, Color color)
    {
        RectTransform rect = Rect(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static void Set(SerializedObject so, string name, Object value) =>
        so.FindProperty(name).objectReferenceValue = value;

    private static void Stretch(RectTransform rect, float margin = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(margin, margin);
        rect.offsetMax = new Vector2(-margin, -margin);
    }

    private static void Anchor(RectTransform rect, float minX, float minY, float maxX, float maxY)
    {
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
