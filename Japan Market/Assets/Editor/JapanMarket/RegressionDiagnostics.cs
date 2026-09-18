using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using JapanMarket.Domain;
using JapanMarket.Gameplay;
using JapanMarket.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class RegressionDiagnostics
{
    const string ActiveKey = "JapanMarket.RegressionDiagnostics.Active";
    const string PhaseKey = "JapanMarket.RegressionDiagnostics.Phase";
    const string ReportPath = "Logs/regression-diagnostics.txt";
    static double started;
    static float doorStartY;
    static int failures;

    static RegressionDiagnostics()
    {
        if (SessionState.GetBool(ActiveKey, false)) Hook();
    }

    public static void Run()
    {
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetInt(PhaseKey, 0);
        failures = 0;
        File.WriteAllText(ReportPath, string.Empty);
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
        Capture("EDIT");
        RenderDoor();
        Hook();
        EditorApplication.EnterPlaymode();
    }

    public static void RenderOnly()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
        RenderDoor();
        EditorApplication.Exit(0);
    }

    static void Hook()
    {
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= Changed;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += Changed;
        started = EditorApplication.timeSinceStartup;
    }

    static void Tick()
    {
        if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
        if (EditorApplication.timeSinceStartup - started < 4d) return;

        if (SessionState.GetInt(PhaseKey, 0) == 0)
        {
            Capture("PLAY BEFORE COMPUTER");
            Object.FindFirstObjectByType<Computer>()?.Interact();

            ValidateToolToggle();

            AutomaticDoor door = Object.FindFirstObjectByType<AutomaticDoor>();
            Transform leaf = GetConfiguredDoorLeaf(door);
            if (door != null && leaf != null)
            {
                doorStartY = leaf.position.y;
                var probe = new GameObject("Door Validation Player");
                probe.tag = "Player";
                Collider collider = probe.AddComponent<BoxCollider>();
                door.SendMessage("OnTriggerEnter", collider, SendMessageOptions.RequireReceiver);
            }
            else
            {
                Require(false, "porta real e folha Cube.010 existem");
            }

            PriceDisplayUI price = Object.FindFirstObjectByType<PriceDisplayUI>(
                FindObjectsInactive.Include);
            if (price != null) price.ShowDisplay(100f, 120f);
            else Require(false, "Price Display existe");

            SessionState.SetInt(PhaseKey, 1);
            started = EditorApplication.timeSinceStartup;
            return;
        }

        Capture("PLAY AFTER COMPUTER");
        ValidateFourFixes();
        EditorApplication.ExitPlaymode();
    }

    static void Changed(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode) started = EditorApplication.timeSinceStartup;
        if (state != PlayModeStateChange.EnteredEditMode) return;
        SessionState.SetBool(ActiveKey, false);
        File.AppendAllText(ReportPath, $"FOUR FIXES FAILURES={failures}\n");
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    static void ValidateToolToggle()
    {
        ToolUser user = Object.FindFirstObjectByType<ToolUser>();
        IToolBelt belt = GameContext.Current?.Services.Resolve<IToolBelt>();
        if (user == null || belt == null)
        {
            Require(false, "ToolUser e ToolBelt existem");
            return;
        }

        user.Deselect();
        bool selected = user.ToggleSelection(0);
        bool deselected = user.ToggleSelection(0);
        Require(selected && deselected && belt.SelectedIndex == -1,
                "pressionar o mesmo slot duas vezes guarda a ferramenta");
    }

    static void ValidateFourFixes()
    {
        AutomaticDoor door = Object.FindFirstObjectByType<AutomaticDoor>();
        Transform leaf = GetConfiguredDoorLeaf(door);
        Require(leaf != null && leaf.position.y > doorStartY + 1f,
                "a porta move o segmento real Cube.010");

        PriceDisplayUI price = Object.FindFirstObjectByType<PriceDisplayUI>(
            FindObjectsInactive.Include);
        Require(price != null && price.gameObject.activeInHierarchy
                              && price.transform.localScale.x > 0.9f,
                "Price Display inativo volta a aparecer");

        ComputerAppHost host = Object.FindFirstObjectByType<ComputerAppHost>();
        Transform header = host != null ? host.transform.Find("Fundo/Tela/Cabeçalho") : null;
        Transform tabs = host != null ? host.transform.Find("Fundo/Tela/Apps") : null;
        Require(header is RectTransform headerRect && Mathf.Abs(headerRect.rect.height - 44f) < 0.1f,
                "cabeçalho do computador mantém 44 px");
        Require(tabs is RectTransform tabsRect && Mathf.Abs(tabsRect.rect.height - 40f) < 0.1f
                                             && tabs.childCount == 5,
                "cinco abas do computador aparecem em 40 px");
    }

    static void Require(bool condition, string description)
    {
        if (condition)
        {
            File.AppendAllText(ReportPath, "PASS " + description + "\n");
            return;
        }

        failures++;
        File.AppendAllText(ReportPath, "FAIL " + description + "\n");
    }

    static Transform FindTransform(string name) =>
        Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate.name == name);

    static Transform GetConfiguredDoorLeaf(AutomaticDoor door) =>
        door != null
            ? typeof(AutomaticDoor).GetField("doorleft",
                  BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(door) as Transform
            : null;

    static void Capture(string phase)
    {
        var report = new StringBuilder();
        report.AppendLine("=== " + phase + " ===");
        report.AppendLine("Scene=" + SceneManager.GetActiveScene().path);

        Transform doors = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate.name == "Doors");
        report.AppendLine("Doors=" + Describe(doors));
        if (doors != null)
        {
            foreach (Transform child in doors.GetComponentsInChildren<Transform>(true))
            {
                string source = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(child));
                var renderer = child.GetComponent<Renderer>();
                var filter = child.GetComponent<MeshFilter>();
                report.Append("  ").Append(PathOf(child))
                    .Append(" active=").Append(child.gameObject.activeInHierarchy)
                    .Append(" local=").Append(child.localPosition.ToString("F3"))
                    .Append(" source=").Append(source);
                if (filter != null) report.Append(" mesh=").Append(filter.sharedMesh?.name ?? "null");
                if (renderer != null) report.Append(" bounds=").Append(renderer.bounds.ToString("F3"));
                report.AppendLine();
            }
        }

        foreach (AutomaticDoor door in Object.FindObjectsByType<AutomaticDoor>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            report.AppendLine("AutomaticDoor=" + Describe(door != null ? door.transform : null));

        foreach (Computer computer in Object.FindObjectsByType<Computer>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var serialized = new SerializedObject(computer);
            var screen = serialized.FindProperty("computerScreen")?.objectReferenceValue as GameObject;
            report.AppendLine("Computer=" + PathOf(computer.transform));
            report.AppendLine("  Screen=" + Describe(screen != null ? screen.transform : null));
            if (screen != null)
            {
                RectTransform rect = screen.transform as RectTransform;
                report.AppendLine("  Rect=" + (rect != null ? rect.rect.ToString() : "not RectTransform"));
                ComputerAppHost host = screen.GetComponent<ComputerAppHost>();
                report.AppendLine("  Host=" + (host != null ? $"enabled={host.enabled}" : "MISSING"));
                report.AppendLine("  Canvas=" + (screen.GetComponent<Canvas>() != null));
                report.AppendLine("  Raycaster=" + (screen.GetComponent<UnityEngine.UI.GraphicRaycaster>() != null));
                foreach (RectTransform child in screen.GetComponentsInChildren<RectTransform>(true))
                {
                    string relative = PathOf(child).Substring(PathOf(screen.transform).Length).TrimStart('/');
                    if (relative.Count(character => character == '/') > 3) continue;
                    report.AppendLine($"    child={relative} active={child.gameObject.activeInHierarchy} " +
                                      $"rect={child.rect} anchored={child.anchoredPosition} scale={child.localScale}");
                }
            }
        }

        PriceDisplayUI price = Object.FindFirstObjectByType<PriceDisplayUI>(FindObjectsInactive.Include);
        report.AppendLine("PriceDisplay=" + Describe(price != null ? price.transform : null));
        if (price != null && price.transform is RectTransform priceRect)
            report.AppendLine($"  anchored={priceRect.anchoredPosition} scale={priceRect.localScale}");

        TutorialManager tutorial = Object.FindFirstObjectByType<TutorialManager>(FindObjectsInactive.Include);
        if (tutorial != null)
            report.AppendLine($"Tutorial index={tutorial.CurrentStepIndex} event={tutorial.CurrentStepData?.requiredEventId}");

        File.AppendAllText(ReportPath, report.ToString());
    }

    static string Describe(Transform transform)
    {
        if (transform == null) return "MISSING";
        return $"{PathOf(transform)} activeSelf={transform.gameObject.activeSelf} " +
               $"activeHierarchy={transform.gameObject.activeInHierarchy} world={transform.position:F3} " +
               $"children={transform.childCount}";
    }

    static void RenderDoor()
    {
        Transform doors = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate.name == "Doors");
        if (doors == null) return;

        Renderer[] renderers = doors.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        int[] layers = doors.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject.layer).ToArray();
        Transform[] transforms = doors.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++) transforms[i].gameObject.layer = 31;

        Color[] colors = { Color.red, Color.green, Color.blue, Color.yellow, Color.magenta };
        Material[][] originalMaterials = renderers.Select(renderer => renderer.sharedMaterials).ToArray();
        Material[] diagnosticMaterials = new Material[renderers.Length];
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        for (int i = 0; i < renderers.Length; i++)
        {
            var material = new Material(shader) { color = colors[i % colors.Length] };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colors[i % colors.Length]);
            diagnosticMaterials[i] = material;
            renderers[i].sharedMaterial = material;
        }

        var cameraObject = new GameObject("Door Diagnostic Camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / (4f / 3f)) * 1.2f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        camera.cullingMask = 1 << 31;

        var lightObject = new GameObject("Door Diagnostic Light");
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 2f;
        lightObject.transform.rotation = Quaternion.Euler(35f, -30f, 0f);

        RenderSide(camera, bounds.center + Vector3.back * 12f, bounds.center, "Logs/door-model-front.png");
        RenderSide(camera, bounds.center + Vector3.forward * 12f, bounds.center, "Logs/door-model-back.png");

        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].sharedMaterials = originalMaterials[i];
            Object.DestroyImmediate(diagnosticMaterials[i]);
        }
        for (int i = 0; i < transforms.Length; i++) transforms[i].gameObject.layer = layers[i];
        Object.DestroyImmediate(cameraObject);
        Object.DestroyImmediate(lightObject);
    }

    static void RenderSide(Camera camera, Vector3 position, Vector3 target, string path)
    {
        camera.transform.position = position;
        camera.transform.LookAt(target, Vector3.up);
        var texture = new RenderTexture(1024, 768, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = texture;
        camera.Render();
        RenderTexture.active = texture;
        var image = new Texture2D(1024, 768, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, 1024, 768), 0, 0);
        image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(image);
        Object.DestroyImmediate(texture);
    }

    static string PathOf(Transform transform)
    {
        if (transform == null) return "MISSING";
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }
}
