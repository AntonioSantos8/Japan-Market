using System;
using System.Linq;
using JapanMarket.Gameplay;
using JapanMarket.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Monta, conecta e valida as telas do encerramento diário no Canvas real da HUD.
/// Idempotente: pode ser executado novamente sem duplicar objetos.
/// </summary>
public static class DayFlowUISetup
{
    private const string ScenePath = "Assets/Scenes/Main.unity";
    private const string RootName = "Day Flow UI";

    private static readonly Color Backdrop = new(0.01f, 0.015f, 0.025f, 0.88f);
    private static readonly Color Window = new(0.035f, 0.05f, 0.075f, 1f);
    private static readonly Color Card = new(0.055f, 0.075f, 0.105f, 1f);
    private static readonly Color Accent = new(0.2f, 0.9f, 0.76f, 1f);
    private static readonly Color Warm = new(1f, 0.7f, 0.22f, 1f);
    private static readonly Color PrimaryText = new(0.96f, 0.98f, 1f, 1f);
    private static readonly Color SecondaryText = new(0.68f, 0.75f, 0.84f, 1f);

    [MenuItem("Japan Market/Setup/Montar fluxo de fim do dia")]
    public static void Run()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Canvas canvas = FindHudCanvas(scene);
        if (canvas == null)
            throw new InvalidOperationException("Canvas Screen Space Overlay da HUD não encontrado.");

        Ensure<UnityEngine.UI.GraphicRaycaster>(canvas.gameObject);
        EnsureEventSystem(scene);
        ConfigureClock(scene);

        TMP_FontAsset font = canvas.GetComponentsInChildren<TMP_Text>(true)
            .Select(text => text.font)
            .FirstOrDefault(candidate => candidate != null)
            ?? TMP_Settings.defaultFontAsset;

        RectTransform root = EnsureRect(canvas.transform, RootName);
        Stretch(root);
        root.gameObject.layer = canvas.gameObject.layer;
        root.SetAsLastSibling();

        BuildDecision(root, font);
        BuildSummary(root, font);

        EditorUtility.SetDirty(root.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"DAY_FLOW_UI_SETUP_DONE Canvas={PathOf(canvas.transform)} " +
                  "Prompt=21:00 Close=00:00");
    }

    [MenuItem("Japan Market/Validation/Validar fluxo de fim do dia")]
    public static void Validate()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Canvas canvas = FindHudCanvas(scene);
        if (canvas == null) throw new Exception("Canvas da HUD ausente.");

        Transform root = canvas.transform.Find(RootName);
        if (root == null) throw new Exception($"'{RootName}' ausente no Canvas da HUD.");
        if (root.GetComponentsInChildren<DayEndDecisionView>(true).Length != 1)
            throw new Exception("A tela de decisão deve existir exatamente uma vez.");
        if (root.GetComponentsInChildren<DaySummaryView>(true).Length != 1)
            throw new Exception("A tela de resumo deve existir exatamente uma vez.");

        RectTransform decisionPanel = root.Find("Day End Decision/Decision Panel") as RectTransform;
        RectTransform summaryPanel = root.Find("Day Summary/Summary Panel") as RectTransform;
        if (decisionPanel == null || summaryPanel == null)
            throw new Exception("Painel de decisão ou resumo ausente.");
        if (decisionPanel.rect.width <= 0f || decisionPanel.rect.height <= 0f
            || summaryPanel.rect.width <= 0f || summaryPanel.rect.height <= 0f)
            throw new Exception("Um dos painéis de fim do dia tem tamanho zero.");

        if (root.GetComponentsInChildren<UnityEngine.UI.Button>(true).Length != 3)
            throw new Exception("Esperava 3 botões: finalizar, continuar aberto e próximo dia.");
        if (canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            throw new Exception("Canvas sem GraphicRaycaster.");

        EventSystem[] systems = UnityEngine.Object.FindObjectsByType<EventSystem>(
            FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(item => item.gameObject.scene == scene).ToArray();
        if (systems.Length != 1 || systems[0].GetComponent<BaseInputModule>() == null)
            throw new Exception($"EventSystem inválido. Encontrados: {systems.Length}.");

        GameContext context = UnityEngine.Object.FindFirstObjectByType<GameContext>(
            FindObjectsInactive.Include);
        if (context == null || context.gameObject.scene != scene)
            throw new Exception("GameContext ausente na Main.");

        SerializedProperty clock = new SerializedObject(context).FindProperty("_clock");
        float prompt = clock.FindPropertyRelative("EndDayPromptHour").floatValue;
        float closing = clock.FindPropertyRelative("ClosingHour").floatValue;
        float end = clock.FindPropertyRelative("EndOfDayHour").floatValue;
        if (!Mathf.Approximately(prompt, 21f)
            || !Mathf.Approximately(closing, 24f)
            || !Mathf.Approximately(end, 24f))
            throw new Exception($"Horários incorretos: prompt={prompt}, close={closing}, end={end}.");

        ValidateReferences(root.GetComponentInChildren<DayEndDecisionView>(true));
        ValidateReferences(root.GetComponentInChildren<DaySummaryView>(true));

        Debug.Log($"DAY_FLOW_UI_VALIDATION_PASS Canvas={PathOf(canvas.transform)} " +
                  $"Buttons=3 EventSystem={PathOf(systems[0].transform)} " +
                  $"Prompt={prompt:00}:00 Close={closing:00}:00");
    }

    private static void BuildDecision(RectTransform root, TMP_FontAsset font)
    {
        RectTransform controller = EnsureRect(root, "Day End Decision");
        Stretch(controller);

        RectTransform panel = EnsureRect(controller, "Decision Panel");
        Stretch(panel);
        UnityEngine.UI.Image dimmer = Ensure<UnityEngine.UI.Image>(panel.gameObject);
        dimmer.color = Backdrop;
        dimmer.raycastTarget = true;

        RectTransform window = EnsureRect(panel, "Decision Window");
        Center(window, new Vector2(760f, 430f), new Vector2(0f, 10f));
        DecoratePanel(window.gameObject, Window, Accent);

        TextMeshProUGUI time = MakeText(window, "Time", font, "21:00", 34f, Warm,
            FontStyles.Bold, TextAlignmentOptions.Center);
        Place(time.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -42f), new Vector2(650f, 48f), new Vector2(0.5f, 1f));

        TextMeshProUGUI title = MakeText(window, "Title", font, "ENCERRAR O DIA?", 42f,
            PrimaryText, FontStyles.Bold, TextAlignmentOptions.Center);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -95f), new Vector2(650f, 60f), new Vector2(0.5f, 1f));

        TextMeshProUGUI message = MakeText(window, "Message", font,
            "Você já pode encerrar o expediente e ver o resumo do dia.\n" +
            "Se continuar aberto, a loja funcionará normalmente até 00:00.",
            22f, SecondaryText, FontStyles.Normal, TextAlignmentOptions.Center);
        message.enableWordWrapping = true;
        Place(message.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -172f), new Vector2(650f, 100f), new Vector2(0.5f, 1f));

        UnityEngine.UI.Button continueButton = MakeButton(window, "Continue Open Button", font,
            "CONTINUAR ABERTO", Card, PrimaryText);
        TextMeshProUGUI continueLabel = continueButton.GetComponentInChildren<TextMeshProUGUI>(true);
        Place((RectTransform)continueButton.transform, new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(-180f, 52f), new Vector2(310f, 70f),
            new Vector2(0.5f, 0f));

        UnityEngine.UI.Button finishButton = MakeButton(window, "Finish Day Button", font,
            "FINALIZAR DIA", Accent, new Color(0.02f, 0.08f, 0.075f, 1f));
        TextMeshProUGUI finishLabel = finishButton.GetComponentInChildren<TextMeshProUGUI>(true);
        Place((RectTransform)finishButton.transform, new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(180f, 52f), new Vector2(310f, 70f),
            new Vector2(0.5f, 0f));

        DayEndDecisionView view = Ensure<DayEndDecisionView>(controller.gameObject);
        SerializedObject serialized = new(view);
        Set(serialized, "_panel", panel.gameObject);
        Set(serialized, "_finishDayButton", finishButton);
        Set(serialized, "_continueOpenButton", continueButton);
        Set(serialized, "_timeText", time);
        Set(serialized, "_titleText", title);
        Set(serialized, "_messageText", message);
        Set(serialized, "_finishDayLabel", finishLabel);
        Set(serialized, "_continueOpenLabel", continueLabel);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        panel.gameObject.SetActive(false);
        controller.gameObject.SetActive(true);
    }

    private static void BuildSummary(RectTransform root, TMP_FontAsset font)
    {
        RectTransform controller = EnsureRect(root, "Day Summary");
        Stretch(controller);

        RectTransform panel = EnsureRect(controller, "Summary Panel");
        Stretch(panel);
        UnityEngine.UI.Image dimmer = Ensure<UnityEngine.UI.Image>(panel.gameObject);
        dimmer.color = Backdrop;
        dimmer.raycastTarget = true;

        RectTransform window = EnsureRect(panel, "Summary Window");
        Center(window, new Vector2(1120f, 820f), Vector2.zero);
        DecoratePanel(window.gameObject, Window, Accent);

        TextMeshProUGUI title = MakeText(window, "Title", font, "RESUMO DO DIA", 40f,
            PrimaryText, FontStyles.Bold, TextAlignmentOptions.Center);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -32f), new Vector2(920f, 56f), new Vector2(0.5f, 1f));

        TextMeshProUGUI day = MakeText(window, "Day", font, "Dia 1 concluído", 21f,
            Accent, FontStyles.Bold, TextAlignmentOptions.Center);
        Place(day.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -87f), new Vector2(920f, 34f), new Vector2(0.5f, 1f));

        RectTransform finance = MakeCard(window, "Finance Card", new Vector2(0f, 0f),
            new Vector2(0.5f, 1f), new Vector2(34f, 126f), new Vector2(-9f, -132f));
        MakeCardHeader(finance, font, "FINANÇAS");
        TextMeshProUGUI opening = MakeStat(finance, font, "Opening Balance", "Saldo inicial: ¥0");
        TextMeshProUGUI revenue = MakeStat(finance, font, "Revenue", "Receita de vendas: ¥0");
        TextMeshProUGUI cost = MakeStat(finance, font, "Cost Of Goods", "Custo dos produtos: ¥0");
        TextMeshProUGUI gross = MakeStat(finance, font, "Gross Profit", "Lucro bruto: ¥0", Accent);
        TextMeshProUGUI purchases = MakeStat(finance, font, "Purchases", "Compras de estoque: ¥0");
        TextMeshProUGUI expenses = MakeStat(finance, font, "Expenses", "Despesas do dia: ¥0");
        TextMeshProUGUI net = MakeStat(finance, font, "Net Profit", "Lucro líquido: ¥0", Warm);
        TextMeshProUGUI closing = MakeStat(finance, font, "Closing Balance", "Saldo final: ¥0");
        TextMeshProUGUI cashFlow = MakeStat(finance, font, "Cash Flow", "Variação de caixa: ¥0");

        RectTransform operation = MakeCard(window, "Operation Card", new Vector2(0.5f, 0f),
            new Vector2(1f, 1f), new Vector2(9f, 126f), new Vector2(-34f, -132f));
        MakeCardHeader(operation, font, "OPERAÇÃO");
        TextMeshProUGUI served = MakeStat(operation, font, "Customers Served", "Clientes atendidos: 0");
        TextMeshProUGUI lost = MakeStat(operation, font, "Customers Lost", "Clientes perdidos: 0");
        TextMeshProUGUI items = MakeStat(operation, font, "Items Sold", "Itens vendidos: 0");
        TextMeshProUGUI average = MakeStat(operation, font, "Average Ticket", "Ticket médio: ¥0");
        TextMeshProUGUI reasons = MakeStat(operation, font, "Loss Reasons", "Motivos de perda: nenhum");
        reasons.enableWordWrapping = true;
        Ensure<UnityEngine.UI.LayoutElement>(reasons.gameObject).preferredHeight = 175f;

        UnityEngine.UI.Button nextButton = MakeButton(window, "Next Day Button", font,
            "COMEÇAR PRÓXIMO DIA", Accent, new Color(0.02f, 0.08f, 0.075f, 1f));
        Place((RectTransform)nextButton.transform, new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(430f, 68f),
            new Vector2(0.5f, 0f));

        DaySummaryView view = Ensure<DaySummaryView>(controller.gameObject);
        SerializedObject serialized = new(view);
        Set(serialized, "_panel", panel.gameObject);
        Set(serialized, "_continueButton", nextButton);
        Set(serialized, "_titleText", title);
        Set(serialized, "_dayText", day);
        Set(serialized, "_openingBalanceText", opening);
        Set(serialized, "_revenueText", revenue);
        Set(serialized, "_costOfGoodsText", cost);
        Set(serialized, "_grossProfitText", gross);
        Set(serialized, "_purchasesText", purchases);
        Set(serialized, "_expensesText", expenses);
        Set(serialized, "_netProfitText", net);
        Set(serialized, "_closingBalanceText", closing);
        Set(serialized, "_cashFlowText", cashFlow);
        Set(serialized, "_customersServedText", served);
        Set(serialized, "_customersLostText", lost);
        Set(serialized, "_itemsSoldText", items);
        Set(serialized, "_averageTicketText", average);
        Set(serialized, "_lossReasonsText", reasons);
        serialized.FindProperty("_pauseWhileOpen").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        panel.gameObject.SetActive(false);
        controller.gameObject.SetActive(true);
    }

    private static RectTransform MakeCard(RectTransform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        RectTransform card = EnsureRect(parent, name);
        card.anchorMin = anchorMin;
        card.anchorMax = anchorMax;
        card.offsetMin = offsetMin;
        card.offsetMax = offsetMax;
        card.pivot = new Vector2(0.5f, 0.5f);
        DecoratePanel(card.gameObject, Card, new Color(0.16f, 0.24f, 0.32f, 1f));

        UnityEngine.UI.VerticalLayoutGroup layout = Ensure<UnityEngine.UI.VerticalLayoutGroup>(card.gameObject);
        layout.padding = new RectOffset(28, 28, 24, 24);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return card;
    }

    private static void MakeCardHeader(RectTransform card, TMP_FontAsset font, string label)
    {
        TextMeshProUGUI header = MakeText(card, "Header", font, label,
            24f, Accent, FontStyles.Bold, TextAlignmentOptions.Left);
        Ensure<UnityEngine.UI.LayoutElement>(header.gameObject).preferredHeight = 38f;
    }

    private static TextMeshProUGUI MakeStat(RectTransform card, TMP_FontAsset font,
        string name, string value, Color? color = null)
    {
        TextMeshProUGUI text = MakeText(card, name, font, value, 20f,
            color ?? PrimaryText, FontStyles.Normal, TextAlignmentOptions.Left);
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        Ensure<UnityEngine.UI.LayoutElement>(text.gameObject).preferredHeight = 38f;
        return text;
    }

    private static UnityEngine.UI.Button MakeButton(RectTransform parent, string name,
        TMP_FontAsset font, string label, Color background, Color foreground)
    {
        RectTransform rect = EnsureRect(parent, name);
        UnityEngine.UI.Image image = Ensure<UnityEngine.UI.Image>(rect.gameObject);
        image.color = background;
        image.raycastTarget = true;

        UnityEngine.UI.Button button = Ensure<UnityEngine.UI.Button>(rect.gameObject);
        button.targetGraphic = image;
        button.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
        UnityEngine.UI.ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        colors.pressedColor = new Color(0.78f, 0.82f, 0.86f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.38f, 0.4f, 0.44f, 0.65f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        TextMeshProUGUI text = MakeText(rect, "Label", font, label, 19f, foreground,
            FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(text.rectTransform, 12f);
        return button;
    }

    private static TextMeshProUGUI MakeText(RectTransform parent, string name,
        TMP_FontAsset font, string value, float size, Color color, FontStyles style,
        TextAlignmentOptions alignment)
    {
        RectTransform rect = EnsureRect(parent, name);
        TextMeshProUGUI text = Ensure<TextMeshProUGUI>(rect.gameObject);
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static void DecoratePanel(GameObject gameObject, Color fill, Color edge)
    {
        UnityEngine.UI.Image image = Ensure<UnityEngine.UI.Image>(gameObject);
        image.color = fill;
        UnityEngine.UI.Outline outline = Ensure<UnityEngine.UI.Outline>(gameObject);
        outline.effectColor = edge;
        outline.effectDistance = new Vector2(2f, -2f);
    }

    private static void ConfigureClock(Scene scene)
    {
        GameContext context = UnityEngine.Object.FindFirstObjectByType<GameContext>(
            FindObjectsInactive.Include);
        if (context == null || context.gameObject.scene != scene)
            throw new InvalidOperationException("GameContext não encontrado na Main.");

        SerializedObject serialized = new(context);
        SerializedProperty clock = serialized.FindProperty("_clock");
        clock.FindPropertyRelative("EndDayPromptHour").floatValue = 21f;
        clock.FindPropertyRelative("ClosingHour").floatValue = 24f;
        clock.FindPropertyRelative("EndOfDayHour").floatValue = 24f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(context);
    }

    private static void EnsureEventSystem(Scene scene)
    {
        EventSystem[] systems = UnityEngine.Object.FindObjectsByType<EventSystem>(
            FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(item => item.gameObject.scene == scene).ToArray();
        if (systems.Length > 1)
            throw new InvalidOperationException(
                $"A Main tem {systems.Length} EventSystems; corrija a duplicação antes do setup.");

        EventSystem system;
        if (systems.Length == 0)
        {
            GameObject gameObject = new("EventSystem", typeof(EventSystem),
                typeof(StandaloneInputModule));
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            system = gameObject.GetComponent<EventSystem>();
        }
        else system = systems[0];

        if (system.GetComponent<BaseInputModule>() == null)
            system.gameObject.AddComponent<StandaloneInputModule>();
    }

    private static Canvas FindHudCanvas(Scene scene)
    {
        PlayerInput player = UnityEngine.Object.FindFirstObjectByType<PlayerInput>(
            FindObjectsInactive.Include);
        if (player != null && player.gameObject.scene == scene)
        {
            Canvas playerCanvas = player.GetComponentsInChildren<Canvas>(true)
                .FirstOrDefault(candidate => candidate.renderMode == RenderMode.ScreenSpaceOverlay);
            if (playerCanvas != null) return playerCanvas;
        }

        return UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate.gameObject.scene == scene
                                      && candidate.renderMode == RenderMode.ScreenSpaceOverlay);
    }

    private static RectTransform EnsureRect(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing is RectTransform rect) return rect;
        if (existing != null)
            throw new InvalidOperationException($"'{PathOf(existing)}' existe sem RectTransform.");

        GameObject child = new(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        child.layer = parent.gameObject.layer;
        return (RectTransform)child.transform;
    }

    private static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static void Center(RectTransform rect, Vector2 size, Vector2 position)
    {
        Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position,
            size, new Vector2(0.5f, 0.5f));
    }

    private static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 position, Vector2 size, Vector2 pivot)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static T Ensure<T>(GameObject gameObject) where T : Component =>
        gameObject.GetComponent<T>() ?? gameObject.AddComponent<T>();

    private static void Set(SerializedObject serialized, string name, UnityEngine.Object value)
    {
        SerializedProperty property = serialized.FindProperty(name)
            ?? throw new InvalidOperationException(
                $"Campo serializado '{name}' ausente em {serialized.targetObject.GetType().Name}.");
        property.objectReferenceValue = value;
    }

    private static void ValidateReferences(Component component)
    {
        SerializedObject serialized = new(component);
        SerializedProperty iterator = serialized.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iterator.propertyType == SerializedPropertyType.ObjectReference
                && iterator.name.StartsWith("_")
                && iterator.objectReferenceValue == null)
                throw new Exception(
                    $"Referência '{iterator.name}' não ligada em {component.GetType().Name}.");
        }
    }

    private static string PathOf(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }
}
