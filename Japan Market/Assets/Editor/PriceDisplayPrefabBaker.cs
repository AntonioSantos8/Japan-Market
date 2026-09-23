#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
internal static class PriceDisplayPrefabBaker
{
    private const string PrefabFolder = "Assets/Prefabs/UI";
    private const string PrefabPath = PrefabFolder + "/PriceDisplay.prefab";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
    private const string RootName = "Price Display";

    private static readonly Color DeepBlue = new(0.02f, 0.13f, 0.17f, 1f);
    private static readonly Color HeaderBlue = new(0.02f, 0.22f, 0.3f, 1f);
    private static readonly Color AccentBlue = new(0.39f, 0.82f, 1f, 1f);
    private static readonly Color InputBlue = new(0.72f, 0.9f, 1f, 1f);
    private static readonly Color White = new(0.97f, 0.98f, 1f, 1f);
    private static readonly Color Muted = new(0.58f, 0.78f, 0.85f, 1f);

    static PriceDisplayPrefabBaker()
    {
        EditorApplication.delayCall += BakeWhenReady;
    }

    [MenuItem("Tools/Japan Market/Recriar Prefab do Display de Preço")]
    private static void BakeFromMenu() => Bake(forceRebuild: true);

    private static void BakeWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        Bake(forceRebuild: false);
    }

    private static void Bake(bool forceRebuild)
    {
        GameObject displayPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (forceRebuild || displayPrefab == null)
            displayPrefab = BuildDisplayPrefab();
        if (displayPrefab == null) return;
        InstallInPlayer(displayPrefab, forceRebuild);
    }

    private static GameObject BuildDisplayPrefab()
    {
        EnsureFolder("Assets/Prefabs", "UI");

        GameObject root = new(RootName, typeof(RectTransform), typeof(Image), typeof(PriceDisplayUI));
        root.layer = LayerMask.NameToLayer("UI");
        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(920f, 620f);
        root.GetComponent<Image>().color = DeepBlue;
        PriceDisplayUI controller = root.GetComponent<PriceDisplayUI>();

        Image header = AddImage(CreateRect("Header", rootRect,
            new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -55f), new Vector2(0f, 110f)), HeaderBlue);
        CreateText("Tag Icon", header.transform, "◆", 48f, AccentBlue, TextAlignmentOptions.Center,
            new Vector2(54f, 0f), new Vector2(70f, 80f), new Vector2(0f, 0.5f));
        CreateText("Title", header.transform, "INSIRA O PREÇO", 40f, White,
            TextAlignmentOptions.MidlineLeft, new Vector2(125f, 0f), new Vector2(620f, 80f),
            new Vector2(0f, 0.5f), FontStyles.Bold);

        Button close = CreateButton("Close", header.transform, "×", HeaderBlue, White,
            new Vector2(1f, 0.5f), new Vector2(-48f, 0f), new Vector2(70f, 70f), 42f);

        RectTransform left = CreateRect("Product", rootRect, Vector2.zero, Vector2.one,
            new Vector2(-315f, -55f), new Vector2(260f, -150f));
        Image productImage = AddImage(CreateRect("Product Image", left, new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -185f), new Vector2(190f, 245f)), Color.white);
        productImage.preserveAspect = true;
        productImage.raycastTarget = false;
        TMP_Text productName = CreateText("Product Name", left, "Produto", 30f, White,
            TextAlignmentOptions.Center, new Vector2(0f, 42f), new Vector2(240f, 55f),
            new Vector2(0.5f, 0f), FontStyles.Bold);

        AddImage(CreateRect("Divider", rootRect, new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(280f, -55f), new Vector2(2f, -145f)), HeaderBlue);

        RectTransform content = CreateRect("Pricing", rootRect, Vector2.zero, Vector2.one,
            new Vector2(135f, -42f), new Vector2(-350f, -170f));

        TMP_Text averageCost = CreateValueRow(content, "Average Cost", "Custo médio", "¥0", 190f);
        TMP_InputField priceInput = CreatePriceInput(content, 120f);
        TMP_Text discount = CreateValueRow(content, "Discount", "Desconto", "0%", 48f);
        Slider slider = CreateDiscountSlider(content, 3f);
        TMP_Text discounted = CreateValueRow(content, "Discounted Price", "Preço com desconto", "¥0", -60f);
        TMP_Text market = CreateValueRow(content, "Market Price", "PREÇO DE MERCADO", "¥0", -130f, Muted, 22f);
        AddImage(CreateRect("Separator", content, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(0f, -170f), new Vector2(0f, 2f)), HeaderBlue);
        TMP_Text profit = CreateValueRow(content, "Profit", "Lucro", "¥0", -215f, White, 32f);

        Button apply = CreateButton("Apply", rootRect, "OK", AccentBlue, HeaderBlue,
            new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(300f, 70f), 30f);
        UnityEventTools.AddPersistentListener(apply.onClick, controller.ApplyAndClose);
        UnityEventTools.AddPersistentListener(close.onClick, controller.CancelAndClose);

        SerializedObject serialized = new(controller);
        SetReference(serialized, "productImage", productImage);
        SetReference(serialized, "productNameText", productName);
        SetReference(serialized, "priceInput", priceInput);
        SetReference(serialized, "discountSlider", slider);
        SetReference(serialized, "averageCostValue", averageCost);
        SetReference(serialized, "discountValue", discount);
        SetReference(serialized, "discountedPriceValue", discounted);
        SetReference(serialized, "marketPriceValue", market);
        SetReference(serialized, "profitValue", profit);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        return prefab;
    }

    private static void InstallInPlayer(GameObject displayPrefab, bool force)
    {
        GameObject player = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            Canvas canvas = player.GetComponentInChildren<Canvas>(true);
            GlobalPrices prices = player.GetComponent<GlobalPrices>();
            if (canvas == null || prices == null)
            {
                Debug.LogError("[Display de preço] Canvas ou GlobalPrices não encontrado no Player.prefab.");
                return;
            }

            PriceDisplayUI current = player.GetComponentInChildren<PriceDisplayUI>(true);
            bool alreadyInstalled = current != null &&
                AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(current.gameObject)) == PrefabPath;
            if (alreadyInstalled && !force) return;

            if (current != null) Object.DestroyImmediate(current.gameObject);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(displayPrefab, canvas.transform);
            instance.name = RootName;
            RectTransform rect = (RectTransform)instance.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -1000f);
            rect.localScale = Vector3.one;

            SerializedObject serializedPrices = new(prices);
            serializedPrices.FindProperty("_priceDisplay").objectReferenceValue =
                instance.GetComponent<PriceDisplayUI>();
            serializedPrices.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(player);
        }
    }

    private static TMP_InputField CreatePriceInput(Transform parent, float y)
    {
        CreateText("Price Label", parent, "Preço", 29f, White, TextAlignmentOptions.MidlineLeft,
            new Vector2(0f, y), new Vector2(260f, 58f), new Vector2(0f, 0.5f), FontStyles.Bold);
        RectTransform inputRect = CreateRect("Price Input", parent, new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f), new Vector2(-125f, y), new Vector2(250f, 66f));
        Image background = AddImage(inputRect, InputBlue);
        TMP_InputField input = inputRect.gameObject.AddComponent<TMP_InputField>();
        TMP_Text text = CreateText("Text", inputRect, "0", 31f, HeaderBlue,
            TextAlignmentOptions.Center, Vector2.zero, new Vector2(-24f, -12f),
            new Vector2(0.5f, 0.5f), FontStyles.Bold);
        text.raycastTarget = true;
        TMP_Text placeholder = CreateText("Placeholder", inputRect, "0", 31f,
            new Color(HeaderBlue.r, HeaderBlue.g, HeaderBlue.b, 0.45f), TextAlignmentOptions.Center,
            Vector2.zero, new Vector2(-24f, -12f), new Vector2(0.5f, 0.5f));
        input.targetGraphic = background;
        input.textComponent = text;
        input.textViewport = inputRect;
        input.placeholder = placeholder;
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.characterLimit = 7;
        return input;
    }

    private static Slider CreateDiscountSlider(Transform parent, float y)
    {
        RectTransform sliderRect = CreateRect("Discount Slider", parent, new Vector2(0f, 0.5f),
            new Vector2(1f, 0.5f), new Vector2(0f, y), new Vector2(-20f, 34f));
        Slider slider = sliderRect.gameObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 90f;
        slider.wholeNumbers = true;
        RectTransform background = CreateRect("Background", sliderRect, new Vector2(0f, 0.5f),
            new Vector2(1f, 0.5f), Vector2.zero, new Vector2(0f, 9f));
        AddImage(background, new Color(0.8f, 0.83f, 0.84f, 1f));
        RectTransform fillArea = CreateRect("Fill Area", sliderRect);
        fillArea.offsetMin = new Vector2(8f, 0f);
        fillArea.offsetMax = new Vector2(-8f, 0f);
        RectTransform fill = CreateRect("Fill", fillArea);
        Stretch(fill);
        AddImage(fill, AccentBlue);
        RectTransform handleArea = CreateRect("Handle Slide Area", sliderRect);
        handleArea.offsetMin = new Vector2(10f, 0f);
        handleArea.offsetMax = new Vector2(-10f, 0f);
        RectTransform handle = CreateRect("Handle", handleArea, new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f), Vector2.zero, new Vector2(24f, 24f));
        Image handleImage = AddImage(handle, White);
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        return slider;
    }

    private static TMP_Text CreateValueRow(Transform parent, string name, string label, string value,
        float y, Color? color = null, float fontSize = 27f)
    {
        CreateText(name + " Label", parent, label, fontSize, color ?? White,
            TextAlignmentOptions.MidlineLeft, new Vector2(0f, y), new Vector2(360f, 54f),
            new Vector2(0f, 0.5f), FontStyles.Bold);
        return CreateText(name + " Value", parent, value, fontSize, color ?? White,
            TextAlignmentOptions.MidlineRight, new Vector2(0f, y), new Vector2(220f, 54f),
            new Vector2(1f, 0.5f), FontStyles.Bold);
    }

    private static Button CreateButton(string name, Transform parent, string label, Color background,
        Color foreground, Vector2 anchor, Vector2 position, Vector2 size, float fontSize)
    {
        RectTransform rect = CreateRect(name, parent, anchor, anchor, position, size);
        Image image = AddImage(rect, background);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        TMP_Text text = CreateText("Label", rect, label, fontSize, foreground,
            TextAlignmentOptions.Center, Vector2.zero, new Vector2(-12f, -8f),
            new Vector2(0.5f, 0.5f), FontStyles.Bold);
        Stretch(text.rectTransform);
        return button;
    }

    private static TMP_Text CreateText(string name, Transform parent, string value, float fontSize,
        Color color, TextAlignmentOptions alignment, Vector2 position, Vector2 size,
        Vector2 anchor, FontStyles style = FontStyles.Normal)
    {
        RectTransform rect = CreateRect(name, parent, anchor, anchor, position, size);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform CreateRect(string name, Transform parent,
        Vector2? anchorMin = null, Vector2? anchorMax = null,
        Vector2? position = null, Vector2? size = null)
    {
        GameObject go = new(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin ?? Vector2.zero;
        rect.anchorMax = anchorMax ?? Vector2.one;
        rect.anchoredPosition = position ?? Vector2.zero;
        rect.sizeDelta = size ?? Vector2.zero;
        rect.localScale = Vector3.one;
        return rect;
    }

    private static Image AddImage(RectTransform rect, Color color)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static void SetReference(SerializedObject serialized, string property,
        Object value) => serialized.FindProperty(property).objectReferenceValue = value;

    private static void EnsureFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
