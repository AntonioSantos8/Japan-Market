using System.Collections.Generic;
using JapanMarket.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace JapanMarket.EditorTools
{
    /// <summary>
    /// Migração de Editor: grava e atualiza a UI do computador no prefab. O host
    /// ainda tem um fallback para saves/projetos cujo prefab não foi reimportado.
    /// </summary>
    [InitializeOnLoad]
    internal static class ComputerUIPrefabBaker
    {
        private const string PrefabPath = "Assets/Prefabs/OBJECTS/Furnitures/Computer.prefab";
        private const string AppsRootName = "Apps (Prefab UI)";

        private static readonly Color DesktopColor = new(0.055f, 0.075f, 0.105f, 1f);
        private static readonly Color WindowColor = new(0.12f, 0.145f, 0.18f, 1f);
        private static readonly Color HeaderColor = new(0.18f, 0.215f, 0.265f, 1f);
        private static readonly Color IconColor = new(0.9f, 0.28f, 0.3f, 1f);
        private static readonly Color TextColor = new(0.94f, 0.96f, 1f, 1f);

        static ComputerUIPrefabBaker()
        {
            EditorApplication.delayCall += BakeWhenReady;
        }

        [MenuItem("Tools/Japan Market/Computer/Gravar UI no Prefab")]
        private static void BakeFromMenu() => Bake(logSuccess: true);

        private static void BakeWhenReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            Bake(logSuccess: false);
        }

        private static void Bake(bool logSuccess)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);

            try
            {
                ComputerAppHost host = prefabRoot.GetComponentInChildren<ComputerAppHost>(true);
                if (host == null)
                {
                    Debug.LogError($"[Computer UI] ComputerAppHost não encontrado em {PrefabPath}.");
                    return;
                }

                Transform uiRoot = host.transform.Find("-----UI-----");
                if (uiRoot == null)
                {
                    Debug.LogError("[Computer UI] O objeto -----UI----- não foi encontrado no prefab.");
                    return;
                }

                Transform existingRoot = uiRoot.Find(AppsRootName);
                bool changed = existingRoot == null;

                if (changed) PrepareDesktopBackground(uiRoot);

                changed |= EnsureWallpapers(uiRoot);

                RectTransform appsRoot = existingRoot as RectTransform
                                         ?? CreateRect(AppsRootName, uiRoot);
                Stretch(appsRoot);

                string[] appNames =
                    { "Mercado", "Preços", "Objetivos", "Banco", "Relatório", "Gestão", "Customização" };
                var views = new List<ComputerAppView>(appNames.Length);

                for (int i = 0; i < appNames.Length; i++)
                {
                    Transform existingApp = appsRoot.Find($"App - {appNames[i]}");
                    ComputerAppView view = existingApp != null
                        ? existingApp.GetComponent<ComputerAppView>()
                        : null;

                    if (view == null)
                    {
                        view = CreateApp(appsRoot, appNames[i], i);
                        changed = true;
                    }

                    changed |= SetIconLayout(view, i);
                    views.Add(view);
                }

                WallpaperThemeController wallpapers = uiRoot.GetComponentInChildren<WallpaperThemeController>(true);
                changed |= EnsureCustomizationContent(views[views.Count - 1], wallpapers);

                SerializedObject serializedHost = new(host);
                SerializedProperty apps = serializedHost.FindProperty("_apps");
                apps.arraySize = views.Count;
                for (int i = 0; i < views.Count; i++)
                    apps.GetArrayElementAtIndex(i).objectReferenceValue = views[i];
                changed |= serializedHost.ApplyModifiedPropertiesWithoutUndo();

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
                    AssetDatabase.SaveAssets();
                }

                if (logSuccess && changed)
                    Debug.Log("[Computer UI] UI gravada no prefab do computador.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void PrepareDesktopBackground(Transform uiRoot)
        {
            UnityEngine.UI.Image background = uiRoot.GetComponentInChildren<UnityEngine.UI.Image>(true);
            if (background == null) return;

            background.name = "Desktop Background";
            background.color = DesktopColor;
            background.raycastTarget = false;
        }

        private static bool EnsureWallpapers(Transform uiRoot)
        {
            Transform desktop = uiRoot.Find("Desktop Background");
            if (desktop == null) return false;

            UnityEngine.UI.Image background = desktop.GetComponent<UnityEngine.UI.Image>();
            if (background == null) return false;

            string[] names = { "Original", "Colinas", "K-On", "Angel", "Angel Rosa", "Operação" };
            string[] paths =
            {
                null,
                "Assets/UIComputer/Wallpapers/WallpaperPc.jpg",
                "Assets/UIComputer/Wallpapers/eb906d169da1bf4f51b5a5a62a2a866a.jpg",
                "Assets/UIComputer/Wallpapers/b0d50885a3e06d5fe5fbd6e4f35d5970.jpg",
                "Assets/UIComputer/Wallpapers/78badc0b9b8bfe803fdf05b8b3efd776.jpg",
                "Assets/UIComputer/Wallpapers/3fcb662b76157ae851b2e1b66a29b301.jpg",
            };

            bool changed = false;
            var objects = new GameObject[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                Transform existing = desktop.Find("Wallpaper - " + names[i]);
                if (existing == null)
                {
                    UnityEngine.UI.Image image = CreateImage("Wallpaper - " + names[i], desktop,
                        i == 0 ? background.color : Color.white);
                    Stretch(image.rectTransform);
                    image.raycastTarget = false;
                    image.gameObject.SetActive(i == 0);
                    image.sprite = i == 0 ? background.sprite
                        : AssetDatabase.LoadAssetAtPath<Sprite>(paths[i]);
                    objects[i] = image.gameObject;
                    changed = true;
                }
                else
                {
                    objects[i] = existing.gameObject;
                }
            }

            Transform decoration = desktop.Find("K-On Pivo");
            if (decoration != null)
            {
                decoration.SetParent(objects[0].transform, false);
                changed = true;
            }

            WallpaperThemeController controller = desktop.GetComponent<WallpaperThemeController>();
            if (controller == null)
            {
                controller = desktop.gameObject.AddComponent<WallpaperThemeController>();
                changed = true;
            }

            if (controller != null)
            {
                SerializedObject serialized = new(controller);
                SerializedProperty options = serialized.FindProperty("_wallpapers");
                if (options.arraySize == 0)
                {
                    options.arraySize = objects.Length;
                    for (int i = 0; i < objects.Length; i++)
                    {
                        SerializedProperty option = options.GetArrayElementAtIndex(i);
                        option.FindPropertyRelative("name").stringValue = names[i];
                        option.FindPropertyRelative("wallpaper").objectReferenceValue = objects[i];
                    }
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }
            }
            return changed;
        }

        private static bool EnsureCustomizationContent(ComputerAppView view,
            WallpaperThemeController wallpapers)
        {
            RectTransform content = view != null
                ? view.transform.Find("Window/Content - Edit Here") as RectTransform
                : null;
            if (content == null || wallpapers == null) return false;

            // A tela é criada uma vez no prefab. Edições feitas depois no Inspector
            // (inclusive animações e filhos dos wallpapers) permanecem intactas.
            if (content.Find("Customization Panel") != null) return false;

            Transform placeholder = content.Find("Placeholder");
            if (placeholder != null) placeholder.gameObject.SetActive(false);

            RectTransform panel = CreateRect("Customization Panel", content);
            Stretch(panel);
            var column = panel.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            column.padding = new RectOffset(16, 16, 16, 16);
            column.spacing = 10f;
            column.childAlignment = TextAnchor.UpperLeft;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            TextMeshProUGUI heading = CreateText("Heading", panel, "Wallpaper do PC", 30f,
                TextAlignmentOptions.MidlineLeft);
            FixedHeight(heading.gameObject, 48f);

            TextMeshProUGUI instruction = CreateText("Instruction", panel,
                "Escolha um tema para mudar o fundo do desktop.", 18f,
                TextAlignmentOptions.MidlineLeft);
            FixedHeight(instruction.gameObject, 32f);

            TextMeshProUGUI status = CreateText("Current Theme", panel,
                "Tema atual: Original", 22f, TextAlignmentOptions.MidlineLeft);
            FixedHeight(status.gameObject, 38f);

            var buttons = new UnityEngine.UI.Button[wallpapers.Count];
            var labels = new TextMeshProUGUI[wallpapers.Count];
            for (int i = 0; i < wallpapers.Count; i++)
            {
                UnityEngine.UI.Image row = CreateImage("Theme - " + wallpapers.GetName(i),
                    panel, new Color(0.17f, 0.19f, 0.23f, 1f));
                row.raycastTarget = false;
                FixedHeight(row.gameObject, 104f);

                var layout = row.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
                layout.padding = new RectOffset(10, 10, 8, 8);
                layout.spacing = 12f;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = true;

                UnityEngine.UI.Image preview = CreateImage("Preview", row.transform,
                    new Color(0.21f, 0.23f, 0.28f, 1f));
                preview.sprite = wallpapers.GetPreview(i);
                preview.preserveAspect = true;
                preview.raycastTarget = false;
                FixedWidth(preview.gameObject, 150f);

                TextMeshProUGUI name = CreateText("Theme Name", row.transform,
                    wallpapers.GetName(i), 22f, TextAlignmentOptions.MidlineLeft);
                name.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1f;

                UnityEngine.UI.Button button = CreateButton("Apply Button", row.transform,
                    new Color(0.95f, 0.36f, 0.38f, 1f));
                FixedWidth(button.gameObject, 150f);
                TextMeshProUGUI label = CreateText("Button Label", button.transform,
                    i == 0 ? "Selecionado" : "Aplicar", 18f, TextAlignmentOptions.Center);
                Stretch(label.rectTransform, 4f);
                button.interactable = i != 0;
                buttons[i] = button;
                labels[i] = label;
            }

            CustomizationApp app = content.GetComponent<CustomizationApp>()
                ?? content.gameObject.AddComponent<CustomizationApp>();
            SerializedObject serializedApp = new(app);
            serializedApp.FindProperty("_wallpapers").objectReferenceValue = wallpapers;
            serializedApp.FindProperty("_status").objectReferenceValue = status;
            SerializedProperty buttonProperty = serializedApp.FindProperty("_buttons");
            SerializedProperty labelProperty = serializedApp.FindProperty("_buttonLabels");
            buttonProperty.arraySize = buttons.Length;
            labelProperty.arraySize = labels.Length;
            for (int i = 0; i < buttons.Length; i++)
            {
                buttonProperty.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
                labelProperty.GetArrayElementAtIndex(i).objectReferenceValue = labels[i];
            }
            serializedApp.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static void FixedHeight(GameObject target, float height)
        {
            var element = target.AddComponent<UnityEngine.UI.LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleHeight = 0f;
        }

        private static void FixedWidth(GameObject target, float width)
        {
            var element = target.AddComponent<UnityEngine.UI.LayoutElement>();
            element.minWidth = width;
            element.preferredWidth = width;
            element.flexibleWidth = 0f;
        }

        private static ComputerAppView CreateApp(RectTransform parent, string appName, int index)
        {
            RectTransform appRoot = CreateRect($"App - {appName}", parent);
            Stretch(appRoot);

            UnityEngine.UI.Button iconButton = CreateButton("Icon Button", appRoot, IconColor);
            RectTransform iconRect = (RectTransform)iconButton.transform;
            iconRect.anchorMin = new Vector2(0f, 1f);
            iconRect.anchorMax = new Vector2(0f, 1f);
            iconRect.pivot = new Vector2(0f, 1f);
            iconRect.anchoredPosition = new Vector2(24f + index * 154f, -34f);
            iconRect.sizeDelta = new Vector2(134f, 126f);

            TextMeshProUGUI initial = CreateText("Icon", iconRect,
                appName.Substring(0, 1).ToUpperInvariant(), 48f, TextAlignmentOptions.Center);
            initial.rectTransform.anchorMin = new Vector2(0f, 0.28f);
            initial.rectTransform.anchorMax = Vector2.one;
            initial.rectTransform.offsetMin = new Vector2(8f, 0f);
            initial.rectTransform.offsetMax = new Vector2(-8f, -8f);

            TextMeshProUGUI iconName = CreateText("App Name", iconRect, appName, 18f,
                TextAlignmentOptions.Center);
            iconName.rectTransform.anchorMin = Vector2.zero;
            iconName.rectTransform.anchorMax = new Vector2(1f, 0.28f);
            iconName.rectTransform.offsetMin = new Vector2(4f, 2f);
            iconName.rectTransform.offsetMax = new Vector2(-4f, -2f);

            UnityEngine.UI.Image window = CreateImage("Window", appRoot, WindowColor);
            Stretch(window.rectTransform, 24f);
            window.gameObject.SetActive(false);

            UnityEngine.UI.Image header = CreateImage("Header", window.transform, HeaderColor);
            RectTransform headerRect = header.rectTransform;
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = Vector2.one;
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.offsetMin = new Vector2(0f, -72f);
            headerRect.offsetMax = Vector2.zero;

            TextMeshProUGUI title = CreateText("Title", headerRect, appName, 30f,
                TextAlignmentOptions.MidlineLeft);
            Stretch(title.rectTransform);
            title.margin = new Vector4(22f, 0f, 90f, 0f);

            UnityEngine.UI.Button closeButton = CreateButton("Close Button", headerRect,
                new Color(0.78f, 0.2f, 0.22f, 1f));
            RectTransform closeRect = (RectTransform)closeButton.transform;
            closeRect.anchorMin = new Vector2(1f, 0f);
            closeRect.anchorMax = Vector2.one;
            closeRect.pivot = new Vector2(1f, 0.5f);
            closeRect.offsetMin = new Vector2(-68f, 8f);
            closeRect.offsetMax = new Vector2(-8f, -8f);
            TextMeshProUGUI closeText = CreateText("Label", closeRect, "×", 36f,
                TextAlignmentOptions.Center);
            Stretch(closeText.rectTransform);

            RectTransform content = CreateRect("Content - Edit Here", window.transform);
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = new Vector2(18f, 18f);
            content.offsetMax = new Vector2(-18f, -90f);

            TextMeshProUGUI placeholder = CreateText("Placeholder", content,
                $"Conteúdo do app {appName}\n\nEdite este objeto no prefab.", 24f,
                TextAlignmentOptions.Center);
            placeholder.color = new Color(TextColor.r, TextColor.g, TextColor.b, 0.55f);
            Stretch(placeholder.rectTransform);

            ComputerAppView view = appRoot.gameObject.AddComponent<ComputerAppView>();
            SerializedObject serializedView = new(view);
            serializedView.FindProperty("_iconButton").objectReferenceValue = iconButton;
            serializedView.FindProperty("_windowRoot").objectReferenceValue = window.gameObject;
            serializedView.FindProperty("_closeButton").objectReferenceValue = closeButton;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        private static bool SetIconLayout(ComputerAppView view, int index)
        {
            if (view == null) return false;

            Transform button = view.transform.Find("Icon Button");
            if (button is not RectTransform rect) return false;

            Vector2 position = new(24f + index * 154f, -34f);
            Vector2 size = new(134f, 126f);
            bool changed = rect.anchoredPosition != position || rect.sizeDelta != size;

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            return changed;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static UnityEngine.UI.Image CreateImage(string name, Transform parent, Color color)
        {
            RectTransform rect = CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            return image;
        }

        private static UnityEngine.UI.Button CreateButton(string name, Transform parent, Color color)
        {
            UnityEngine.UI.Image image = CreateImage(name, parent, color);
            var button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            return button;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string value,
            float size, TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRect(name, parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.color = TextColor;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect, float margin = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(margin, margin);
            rect.offsetMax = new Vector2(-margin, -margin);
        }
    }
}
