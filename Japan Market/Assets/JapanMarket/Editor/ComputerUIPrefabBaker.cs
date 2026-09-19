using System.Collections.Generic;
using JapanMarket.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace JapanMarket.EditorTools
{
    /// <summary>
    /// Migração de Editor: grava a UI do computador no prefab. Depois disso a
    /// hierarquia fica totalmente editável e nenhuma UI é criada em runtime.
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

                if (uiRoot.Find(AppsRootName) != null)
                    return;

                PrepareDesktopBackground(uiRoot);

                RectTransform appsRoot = CreateRect(AppsRootName, uiRoot);
                Stretch(appsRoot);

                string[] appNames = { "Mercado", "Preços", "Objetivos", "Banco", "Relatório" };
                var views = new List<ComputerAppView>(appNames.Length);

                for (int i = 0; i < appNames.Length; i++)
                    views.Add(CreateApp(appsRoot, appNames[i], i));

                SerializedObject serializedHost = new(host);
                SerializedProperty apps = serializedHost.FindProperty("_apps");
                apps.arraySize = views.Count;
                for (int i = 0; i < views.Count; i++)
                    apps.GetArrayElementAtIndex(i).objectReferenceValue = views[i];
                serializedHost.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
                AssetDatabase.SaveAssets();

                if (logSuccess)
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

        private static ComputerAppView CreateApp(RectTransform parent, string appName, int index)
        {
            RectTransform appRoot = CreateRect($"App - {appName}", parent);
            Stretch(appRoot);

            UnityEngine.UI.Button iconButton = CreateButton("Icon Button", appRoot, IconColor);
            RectTransform iconRect = (RectTransform)iconButton.transform;
            iconRect.anchorMin = new Vector2(0f, 1f);
            iconRect.anchorMax = new Vector2(0f, 1f);
            iconRect.pivot = new Vector2(0f, 1f);
            iconRect.anchoredPosition = new Vector2(34f + index * 174f, -34f);
            iconRect.sizeDelta = new Vector2(140f, 126f);

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
