using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Montagem idempotente do rastreador de objetivos na HUD da Main.</summary>
public static class ObjectiveHudSetup
{
    private const string ScenePath = "Assets/Scenes/Main.unity";
    private const string RootName = "Objective Tracker";

    [MenuItem("Japan Market/Setup/Montar HUD de objetivos")]
    public static void Run()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Canvas canvas = FindHudCanvas(scene);

        if (canvas == null)
            throw new System.InvalidOperationException(
                "Nenhum Canvas Screen Space Overlay existente foi encontrado na Main.");

        RectTransform root = EnsureRect(canvas.transform, RootName);
        root.gameObject.layer = canvas.gameObject.layer;
        root.anchorMin = Vector2.one;
        root.anchorMax = Vector2.one;
        root.pivot = Vector2.one;
        root.anchoredPosition = new Vector2(-32f, -32f);
        root.sizeDelta = new Vector2(440f, 300f);
        root.SetAsLastSibling();

        UnityEngine.UI.Image background = Ensure<UnityEngine.UI.Image>(root.gameObject);
        background.color = new Color(0.025f, 0.035f, 0.05f, 0.92f);
        background.raycastTarget = false;

        UnityEngine.UI.Outline outline = Ensure<UnityEngine.UI.Outline>(root.gameObject);
        outline.effectColor = new Color(0.22f, 0.75f, 0.72f, 0.75f);
        outline.effectDistance = new Vector2(2f, -2f);

        UnityEngine.UI.VerticalLayoutGroup layout =
            Ensure<UnityEngine.UI.VerticalLayoutGroup>(root.gameObject);
        layout.padding = new RectOffset(22, 22, 18, 18);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        TMP_FontAsset font = canvas.GetComponentsInChildren<TMP_Text>(true)
            .Select(text => text.font)
            .FirstOrDefault(candidate => candidate != null)
            ?? TMP_Settings.defaultFontAsset;

        TextMeshProUGUI header = EnsureText(root, "Header", font, 24f,
            new Color(0.3f, 1f, 0.94f, 1f), FontStyles.Bold);
        UnityEngine.UI.LayoutElement headerLayout =
            Ensure<UnityEngine.UI.LayoutElement>(header.gameObject);
        headerLayout.preferredHeight = 34f;
        header.text = "OBJECTIVE";

        RectTransform dividerRect = EnsureRect(root, "Divider");
        dividerRect.gameObject.layer = canvas.gameObject.layer;
        UnityEngine.UI.Image divider = Ensure<UnityEngine.UI.Image>(dividerRect.gameObject);
        divider.color = new Color(0.3f, 1f, 0.94f, 0.55f);
        divider.raycastTarget = false;
        UnityEngine.UI.LayoutElement dividerLayout =
            Ensure<UnityEngine.UI.LayoutElement>(dividerRect.gameObject);
        dividerLayout.preferredHeight = 2f;

        TextMeshProUGUI body = EnsureText(root, "Body", font, 20f,
            new Color(0.94f, 0.96f, 1f, 1f), FontStyles.Normal);
        body.text = "Finish tutorial";
        body.enableWordWrapping = true;
        body.overflowMode = TextOverflowModes.Ellipsis;
        body.lineSpacing = 6f;
        UnityEngine.UI.LayoutElement bodyLayout =
            Ensure<UnityEngine.UI.LayoutElement>(body.gameObject);
        bodyLayout.preferredHeight = 208f;

        ObjectiveTrackerUI tracker = Ensure<ObjectiveTrackerUI>(root.gameObject);
        SerializedObject serialized = new SerializedObject(tracker);
        serialized.FindProperty("headerText").objectReferenceValue = header;
        serialized.FindProperty("bodyText").objectReferenceValue = body;
        serialized.FindProperty("tutorialObjective").stringValue = "Finish tutorial";
        serialized.FindProperty("maxVisibleObjectives").intValue = 3;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(root.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"OBJECTIVE_HUD_SETUP_DONE Canvas={GetPath(canvas.transform)} " +
                  $"Size={root.rect.size}");
    }

    public static void Validate()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Canvas canvas = FindHudCanvas(scene);
        if (canvas == null) throw new System.Exception("HUD Canvas ausente.");

        RectTransform[] trackers = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<RectTransform>(true))
            .Where(rect => rect.name == RootName)
            .ToArray();
        if (trackers.Length != 1)
            throw new System.Exception($"Esperava 1 '{RootName}', encontrei {trackers.Length}.");

        RectTransform trackerRect = trackers[0];
        if (trackerRect.parent != canvas.transform)
            throw new System.Exception("Objective Tracker não está no Canvas da HUD.");
        if (!trackerRect.gameObject.activeInHierarchy)
            throw new System.Exception("Objective Tracker está inativo.");
        if (trackerRect.rect.width <= 0f || trackerRect.rect.height <= 0f)
            throw new System.Exception("Objective Tracker tem tamanho zero.");

        ObjectiveTrackerUI tracker = trackerRect.GetComponent<ObjectiveTrackerUI>();
        TextMeshProUGUI header = trackerRect.Find("Header")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI body = trackerRect.Find("Body")?.GetComponent<TextMeshProUGUI>();
        if (tracker == null || header == null || body == null)
            throw new System.Exception("Componentes obrigatórios do Objective Tracker ausentes.");
        if (body.text != "Finish tutorial")
            throw new System.Exception($"Objetivo inicial incorreto: '{body.text}'.");
        if (header.raycastTarget || body.raycastTarget)
            throw new System.Exception("Textos decorativos estão bloqueando raycasts da HUD.");
        if (canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            throw new System.Exception("Canvas da HUD está sem GraphicRaycaster.");

        SerializedObject serialized = new SerializedObject(tracker);
        if (serialized.FindProperty("headerText").objectReferenceValue == null
            || serialized.FindProperty("bodyText").objectReferenceValue == null)
            throw new System.Exception("Referências do ObjectiveTrackerUI não foram serializadas.");

        Debug.Log($"OBJECTIVE_HUD_VALIDATION_PASS Canvas={GetPath(canvas.transform)} " +
                  $"Rect={trackerRect.rect.size} Initial='{body.text}'");
    }

    private static Canvas FindHudCanvas(Scene scene)
    {
        PlayerInput player = Object.FindFirstObjectByType<PlayerInput>(FindObjectsInactive.Include);
        if (player != null && player.gameObject.scene == scene)
        {
            Canvas playerCanvas = player.GetComponentsInChildren<Canvas>(true)
                .FirstOrDefault(candidate => candidate.renderMode == RenderMode.ScreenSpaceOverlay);
            if (playerCanvas != null) return playerCanvas;
        }

        return Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate.gameObject.scene == scene
                                      && candidate.renderMode == RenderMode.ScreenSpaceOverlay);
    }

    private static RectTransform EnsureRect(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing is RectTransform rect) return rect;

        var child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return (RectTransform)child.transform;
    }

    private static TextMeshProUGUI EnsureText(RectTransform parent, string name,
                                               TMP_FontAsset font, float size,
                                               Color color, FontStyles style)
    {
        RectTransform rect = EnsureRect(parent, name);
        rect.gameObject.layer = parent.gameObject.layer;

        TextMeshProUGUI text = Ensure<TextMeshProUGUI>(rect.gameObject);
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.raycastTarget = false;
        return text;
    }

    private static T Ensure<T>(GameObject gameObject) where T : Component =>
        gameObject.GetComponent<T>() ?? gameObject.AddComponent<T>();

    private static string GetPath(Transform transform)
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
