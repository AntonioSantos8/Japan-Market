#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class OptionsMenuBuilder
{
    private const string ScenePath = "Assets/Utilidade/Scenes/MainMenuTemplate.unity";
    private const string PrefabFolder = "Assets/Utilidade/Prefabs";
    private const string PrefabPath = PrefabFolder + "/OptionsMenu.prefab";

    private static readonly Color Ink = new(0.055f, 0.071f, 0.09f, 1f);
    private static readonly Color Panel = new(0.09f, 0.11f, 0.14f, 0.98f);
    private static readonly Color Surface = new(0.14f, 0.16f, 0.19f, 1f);
    private static readonly Color Soft = new(0.66f, 0.68f, 0.7f, 1f);
    private static readonly Color Paper = new(0.94f, 0.92f, 0.86f, 1f);
    private static readonly Color Red = new(0.82f, 0.13f, 0.16f, 1f);
    private static readonly Color Gold = new(0.91f, 0.68f, 0.25f, 1f);

    static OptionsMenuBuilder()
    {
        EditorApplication.delayCall += BuildIfNeeded;
    }

    [MenuItem("Tools/Japan Market/Recriar Menu de Opcoes")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        bool alreadyOpen = SceneManager.GetSceneByPath(ScenePath).isLoaded;
        Scene scene = alreadyOpen
            ? SceneManager.GetSceneByPath(ScenePath)
            : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        try
        {
            Canvas canvas = FindInScene<Canvas>(scene);
            if (canvas == null) throw new InvalidOperationException("Canvas nao encontrado em " + ScenePath);

            Transform oldRoot = FindChildRecursive(canvas.transform, "Options Menu");
            GameObject root = oldRoot != null ? oldRoot.gameObject : CreateRect("Options Menu", canvas.transform).gameObject;

            root.SetActive(true);
            root.layer = LayerMask.NameToLayer("UI");
            RectTransform rootRect = EnsureRect(root);
            Stretch(rootRect);

            for (int i = root.transform.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            {
                if (sceneRoot != root && sceneRoot.name == "OptionsManager")
                    UnityEngine.Object.DestroyImmediate(sceneRoot);
            }

            OptionsManager manager = root.GetComponent<OptionsManager>();
            if (manager == null) manager = root.AddComponent<OptionsManager>();

            Image dim = AddImage(CreateRect("Dim Background", root.transform), new Color(0.015f, 0.02f, 0.028f, 0.88f));
            Stretch(dim.rectTransform);

            RectTransform window = CreateRect("Window", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 820f));
            AddImage(window, Panel);
            AddImage(CreateRect("Red Accent", window, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(7f, 0f), new Vector2(14f, 0f)), Red);

            CreateText("Title", window, "OPCOES", 48, FontStyles.Bold, Paper, TextAlignmentOptions.Left,
                new Vector2(48f, -42f), new Vector2(700f, 62f), new Vector2(0f, 1f));
            CreateText("Subtitle", window, "Ajuste a experiencia do seu mercado", 21, FontStyles.Normal, Soft, TextAlignmentOptions.Left,
                new Vector2(50f, -102f), new Vector2(720f, 38f), new Vector2(0f, 1f));
            CreateText("Japanese Detail", window, "環境設定", 25, FontStyles.Bold, new Color(1f, 1f, 1f, 0.18f), TextAlignmentOptions.Right,
                new Vector2(-48f, -55f), new Vector2(300f, 42f), new Vector2(1f, 1f));

            RectTransform tabsBar = CreateRect("Tabs", window, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -162f), new Vector2(-96f, 64f));
            GameObject[] pages = new GameObject[3];
            string[] tabNames = { "GERAL", "AUDIO", "ACESSIBILIDADE" };

            RectTransform content = CreateRect("Content", window, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, -54f), new Vector2(-96f, -300f));
            for (int i = 0; i < pages.Length; i++)
            {
                int tabIndex = i;
                Button tab = CreateButton("Tab " + tabNames[i], tabsBar, tabNames[i], Surface, Paper,
                    new Vector2(i / 3f, 0f), new Vector2((i + 1) / 3f, 1f), Vector2.zero, new Vector2(-8f, 0f));
                UnityEventTools.AddIntPersistentListener(tab.onClick, manager.SetTab, tabIndex);

                RectTransform page = CreateRect(tabNames[i] + " Page", content);
                Stretch(page);
                pages[i] = page.gameObject;
                page.gameObject.SetActive(i == 0);
            }

            TMP_Dropdown screenMode = CreateSettingDropdown(pages[0].transform, "MODO DE TELA",
                "Escolha como o jogo ocupa o monitor", 115f,
                new[] { "Janela sem borda", "Tela cheia exclusiva", "Janela maximizada", "Janela" });
            Toggle vSync = CreateSettingToggle(pages[0].transform, "SINCRONIZACAO VERTICAL",
                "Evita cortes na imagem ao sincronizar os quadros", -10f, true);
            Toggle showFps = CreateSettingToggle(pages[0].transform, "MOSTRAR FPS",
                "Exibe a taxa de quadros durante o jogo", -135f, false);

            TMP_Text masterValue;
            TMP_Text musicValue;
            TMP_Text sfxValue;
            Slider master = CreateSettingSlider(pages[1].transform, "VOLUME GERAL", "Controla todo o audio do jogo", 110f, out masterValue);
            Slider music = CreateSettingSlider(pages[1].transform, "MUSICA", "Trilha sonora e ambiente musical", -15f, out musicValue);
            Slider sfx = CreateSettingSlider(pages[1].transform, "EFEITOS SONOROS", "Interface, objetos e sons do mercado", -140f, out sfxValue);

            TMP_Dropdown colorBlind = CreateSettingDropdown(pages[2].transform, "FILTRO DE DALTONISMO",
                "Ajuste as cores para melhorar a leitura visual", 100f,
                new[] { "Desativado", "Tritanopia", "Protanopia", "Deuteranopia" });
            CreateInfoCard(pages[2].transform, "PRE-VISUALIZACAO IMEDIATA",
                "As alteracoes visuais e de audio sao aplicadas enquanto voce ajusta. Use Cancelar para voltar aos valores salvos.", -70f);

            TMP_Text status = CreateText("Status", window, string.Empty, 18, FontStyles.Normal, Gold, TextAlignmentOptions.Left,
                new Vector2(50f, 52f), new Vector2(520f, 40f), new Vector2(0f, 0f));

            Button defaults = CreateButton("Restore Defaults", window, "RESTAURAR PADROES", Surface, Paper,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(178f, 55f), new Vector2(255f, 54f));
            Button previous = CreateButton("Previous", window, "<", Surface, Paper,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-80f, 55f), new Vector2(54f, 54f));
            Button next = CreateButton("Next", window, ">", Surface, Paper,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-16f, 55f), new Vector2(54f, 54f));
            Button cancel = CreateButton("Cancel", window, "CANCELAR", Surface, Paper,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-300f, 55f), new Vector2(190f, 54f));
            Button save = CreateButton("Save", window, "SALVAR", Red, Color.white,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-97f, 55f), new Vector2(190f, 54f));

            UnityEventTools.AddPersistentListener(defaults.onClick, manager.RestoreDefaults);
            UnityEventTools.AddPersistentListener(previous.onClick, manager.Previous);
            UnityEventTools.AddPersistentListener(next.onClick, manager.Next);
            UnityEventTools.AddPersistentListener(cancel.onClick, manager.CancelAndClose);
            UnityEventTools.AddPersistentListener(save.onClick, manager.SaveAndClose);

            SerializedObject serialized = new(manager);
            SerializedProperty tabs = serialized.FindProperty("gameObj");
            tabs.arraySize = pages.Length;
            for (int i = 0; i < pages.Length; i++) tabs.GetArrayElementAtIndex(i).objectReferenceValue = pages[i];
            SetReference(serialized, "showFps", showFps);
            SetReference(serialized, "vSync", vSync);
            SetReference(serialized, "screenMode", screenMode);
            SetReference(serialized, "colorBlind", colorBlind);
            SetReference(serialized, "master", master);
            SetReference(serialized, "music", music);
            SetReference(serialized, "sfx", sfx);
            SetReference(serialized, "masterValue", masterValue);
            SetReference(serialized, "musicValue", musicValue);
            SetReference(serialized, "sfxValue", sfxValue);
            SetReference(serialized, "statusText", status);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EnsureFolder();
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);
            HookOpenButton(canvas, root.transform, manager);
            root.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Menu de opcoes criado e salvo em " + PrefabPath);
        }
        finally
        {
            if (!alreadyOpen && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void BuildIfNeeded()
    {
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)) Build();
    }

    private static void HookOpenButton(Canvas canvas, Transform optionsRoot, OptionsManager manager)
    {
        foreach (Button button in canvas.GetComponentsInChildren<Button>(true))
        {
            if (button.transform.IsChildOf(optionsRoot)) continue;
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            string identity = (button.name + " " + (label != null ? label.text : string.Empty)).ToLowerInvariant();
            if (!identity.Contains("option") && !identity.Contains("config")) continue;
            UnityEventTools.AddPersistentListener(button.onClick, manager.Open);
            EditorUtility.SetDirty(button);
            break;
        }
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T result = root.GetComponentInChildren<T>(true);
            if (result != null) return result;
        }
        return null;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName) return child;
            Transform found = FindChildRecursive(child, childName);
            if (found != null) return found;
        }
        return null;
    }

    private static Toggle CreateSettingToggle(Transform parent, string title, string description, float y, bool value)
    {
        RectTransform row = CreateRect(title, parent, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, y), new Vector2(0f, 105f));
        AddImage(row, Surface);
        CreateText("Label", row, title, 24, FontStyles.Bold, Paper, TextAlignmentOptions.Left,
            new Vector2(28f, 22f), new Vector2(750f, 34f), new Vector2(0f, 0.5f));
        CreateText("Description", row, description, 17, FontStyles.Normal, Soft, TextAlignmentOptions.Left,
            new Vector2(28f, -20f), new Vector2(780f, 30f), new Vector2(0f, 0.5f));

        RectTransform toggleRect = CreateRect("Toggle", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-60f, 0f), new Vector2(70f, 38f));
        Toggle toggle = toggleRect.gameObject.AddComponent<Toggle>();
        Image background = AddImage(toggleRect, Ink);
        RectTransform checkRect = CreateRect("Checkmark", toggleRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(19f, 0f), new Vector2(30f, 30f));
        Image check = AddImage(checkRect, Red);
        toggle.targetGraphic = background;
        toggle.graphic = check;
        toggle.isOn = value;
        return toggle;
    }

    private static Slider CreateSettingSlider(Transform parent, string title, string description, float y, out TMP_Text valueText)
    {
        RectTransform row = CreateRect(title, parent, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, y), new Vector2(0f, 105f));
        AddImage(row, Surface);
        CreateText("Label", row, title, 23, FontStyles.Bold, Paper, TextAlignmentOptions.Left,
            new Vector2(28f, 22f), new Vector2(420f, 34f), new Vector2(0f, 0.5f));
        CreateText("Description", row, description, 16, FontStyles.Normal, Soft, TextAlignmentOptions.Left,
            new Vector2(28f, -20f), new Vector2(480f, 28f), new Vector2(0f, 0.5f));

        RectTransform sliderRect = CreateRect("Slider", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-255f, 0f), new Vector2(380f, 38f));
        Slider slider = sliderRect.gameObject.AddComponent<Slider>();
        slider.minValue = 0.0001f;
        slider.maxValue = 1f;
        slider.value = 0.8f;

        RectTransform background = CreateRect("Background", sliderRect, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(0f, 8f));
        AddImage(background, Ink);
        RectTransform fillArea = CreateRect("Fill Area", sliderRect, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(-8f, 0f), new Vector2(-16f, 0f));
        RectTransform fill = CreateRect("Fill", fillArea);
        Stretch(fill);
        Image fillImage = AddImage(fill, Red);
        RectTransform handleArea = CreateRect("Handle Slide Area", sliderRect, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-20f, 0f));
        RectTransform handle = CreateRect("Handle", handleArea, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28f, 28f));
        Image handleImage = AddImage(handle, Paper);
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;

        valueText = CreateText("Value", row, "80%", 21, FontStyles.Bold, Gold, TextAlignmentOptions.Right,
            new Vector2(-28f, 0f), new Vector2(90f, 36f), new Vector2(1f, 0.5f));
        return slider;
    }

    private static TMP_Dropdown CreateSettingDropdown(Transform parent, string title, string description, float y, string[] options)
    {
        RectTransform row = CreateRect(title, parent, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, y), new Vector2(0f, 120f));
        AddImage(row, Surface);
        CreateText("Label", row, title, 23, FontStyles.Bold, Paper, TextAlignmentOptions.Left,
            new Vector2(28f, 22f), new Vector2(600f, 34f), new Vector2(0f, 0.5f));
        CreateText("Description", row, description, 16, FontStyles.Normal, Soft, TextAlignmentOptions.Left,
            new Vector2(28f, -20f), new Vector2(650f, 28f), new Vector2(0f, 0.5f));

        RectTransform dropdownRect = CreateRect("Dropdown", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-235f, 0f), new Vector2(410f, 54f));
        Image dropdownImage = AddImage(dropdownRect, Ink);
        TMP_Dropdown dropdown = dropdownRect.gameObject.AddComponent<TMP_Dropdown>();
        TMP_Text caption = CreateText("Label", dropdownRect, options[0], 18, FontStyles.Normal, Paper, TextAlignmentOptions.Left,
            new Vector2(18f, 0f), new Vector2(-58f, 0f), new Vector2(0f, 0.5f));
        StretchVertical(caption.rectTransform, 8f);
        CreateText("Arrow", dropdownRect, "v", 18, FontStyles.Bold, Gold, TextAlignmentOptions.Center,
            new Vector2(-25f, 0f), new Vector2(32f, 32f), new Vector2(1f, 0.5f));

        RectTransform template = CreateRect("Template", dropdownRect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, -112f), new Vector2(0f, 160f));
        Image templateImage = AddImage(template, Ink);
        ScrollRect scroll = template.gameObject.AddComponent<ScrollRect>();
        RectTransform viewport = CreateRect("Viewport", template);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform content = CreateRect("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -80f), new Vector2(0f, 160f));
        RectTransform item = CreateRect("Item", content, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -20f), new Vector2(0f, 40f));
        Toggle itemToggle = item.gameObject.AddComponent<Toggle>();
        Image itemBackground = AddImage(item, Surface);
        TMP_Text itemLabel = CreateText("Item Label", item, "Opcao", 17, FontStyles.Normal, Paper, TextAlignmentOptions.Left,
            new Vector2(18f, 0f), new Vector2(-36f, 0f), new Vector2(0f, 0.5f));
        StretchVertical(itemLabel.rectTransform, 3f);
        RectTransform checkmark = CreateRect("Item Checkmark", item, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(8f, 24f));
        Image checkImage = AddImage(checkmark, Red);
        itemToggle.targetGraphic = itemBackground;
        itemToggle.graphic = checkImage;
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        dropdown.targetGraphic = dropdownImage;
        dropdown.captionText = caption;
        dropdown.template = template;
        dropdown.itemText = itemLabel;
        dropdown.options = new List<TMP_Dropdown.OptionData>();
        foreach (string option in options) dropdown.options.Add(new TMP_Dropdown.OptionData(option));
        dropdown.SetValueWithoutNotify(0);
        dropdown.RefreshShownValue();
        template.gameObject.SetActive(false);
        return dropdown;
    }

    private static void CreateInfoCard(Transform parent, string title, string body, float y)
    {
        RectTransform card = CreateRect("Info", parent, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, y), new Vector2(0f, 150f));
        AddImage(card, Surface);
        AddImage(CreateRect("Accent", card, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(4f, 0f), new Vector2(8f, 0f)), Gold);
        CreateText("Title", card, title, 22, FontStyles.Bold, Paper, TextAlignmentOptions.Left,
            new Vector2(30f, 34f), new Vector2(-60f, 34f), new Vector2(0f, 0.5f));
        CreateText("Body", card, body, 18, FontStyles.Normal, Soft, TextAlignmentOptions.Left,
            new Vector2(30f, -20f), new Vector2(-60f, 64f), new Vector2(0f, 0.5f));
    }

    private static Button CreateButton(string name, Transform parent, string label, Color background, Color foreground,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        RectTransform rect = CreateRect(name, parent, anchorMin, anchorMax, position, size);
        Image image = AddImage(rect, background);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.82f);
        colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        button.colors = colors;
        TMP_Text text = CreateText("Label", rect, label, 18, FontStyles.Bold, foreground, TextAlignmentOptions.Center, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        Stretch(text.rectTransform);
        return button;
    }

    private static TMP_Text CreateText(string name, Transform parent, string value, float size, FontStyles style, Color color,
        TextAlignmentOptions alignment, Vector2 position, Vector2 dimensions, Vector2 anchor)
    {
        RectTransform rect = CreateRect(name, parent, anchor, anchor, position, dimensions);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2? anchorMin = null, Vector2? anchorMax = null,
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

    private static RectTransform EnsureRect(GameObject go)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        if (rect == null) rect = go.AddComponent<RectTransform>();
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static void StretchVertical(RectTransform rect, float inset)
    {
        rect.anchorMin = new Vector2(rect.anchorMin.x, 0f);
        rect.anchorMax = new Vector2(rect.anchorMax.x, 1f);
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, -inset * 2f);
    }

    private static void SetReference(SerializedObject serialized, string property, UnityEngine.Object value)
    {
        serialized.FindProperty(property).objectReferenceValue = value;
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Utilidade")) AssetDatabase.CreateFolder("Assets", "Utilidade");
        if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/Utilidade", "Prefabs");
    }
}
#endif
