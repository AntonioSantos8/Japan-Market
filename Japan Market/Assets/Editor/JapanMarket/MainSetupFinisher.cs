using System;
using System.IO;
using System.Linq;
using JapanMarket.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Completa as ligações de cena que não podem ser feitas só pelos serviços:
/// entrada/porta, telas, HUD, cursor e referências de scripts já apagados.
/// É idempotente e também fica disponível no menu para futuras remontagens.
/// </summary>
public static class MainSetupFinisher
{
    const string MainScene = "Assets/Scenes/Main.unity";
    const string DoorPrefab = "Assets/Prefabs/DoorAutomatic.prefab";
    const string CursorTexture = "Assets/Sprites/kenney_cursor-pack/PNG/Basic/Double/pointer_d.png";
    const string RequestFile = "Temp/JapanMarketFinishSetup.request";
    const string ReportFile = "Logs/main-setup-finish.txt";

    [InitializeOnLoadMethod]
    static void ResumeRequestedSetup()
    {
        if (!File.Exists(RequestFile)) return;
        EditorApplication.delayCall += TryRunRequestedSetup;
    }

    static void TryRunRequestedSetup()
    {
        if (!File.Exists(RequestFile)) return;

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryRunRequestedSetup;
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.ExitPlaymode();
            EditorApplication.delayCall += TryRunRequestedSetup;
            return;
        }

        Run();
    }

    [MenuItem("Japan Market/Setup/Concluir setup da Main")]
    public static void Run()
    {
        string[] report;

        try
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != MainScene)
                scene = EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);

            int missingScripts = RemoveMissingScripts(scene);
            AutomaticDoor door = EnsureEntranceDoor(scene);
            int computers = EnsureComputerApps(scene);
            bool toolBar = EnsureToolBar(scene);
            bool cursor = RepairCursor();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            report = new[]
            {
                "MAIN_SETUP_FINISH PASS",
                $"Scene: {scene.path}",
                $"Door: {(door != null ? GetPath(door.transform) : "MISSING")}",
                $"ComputerAppHost: {computers}",
                $"ToolWheelView: {(toolBar ? "OK" : "MISSING")}",
                $"Cursor importer: {(cursor ? "OK" : "MISSING")}",
                $"Missing scripts removed: {missingScripts}",
                DateTime.Now.ToString("O")
            };

            File.Delete(RequestFile);
            Debug.Log("[Setup] Main concluída. Porta, tutorial, telas, HUD e cursor ligados.");
        }
        catch (Exception exception)
        {
            report = new[]
            {
                "MAIN_SETUP_FINISH FAIL",
                exception.ToString(),
                DateTime.Now.ToString("O")
            };
            Debug.LogException(exception);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(ReportFile) ?? "Logs");
        File.WriteAllLines(ReportFile, report);
    }

    static AutomaticDoor EnsureEntranceDoor(Scene scene)
    {
        Transform market = FindSceneTransform(scene, "Market");
        Transform store = FindSceneTransform(scene, "LojaCartoon");
        Transform doors = store != null
            ? store.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name == "Doors")
            : null;
        Transform realLeaf = doors != null
            ? doors.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name == "Cube.010")
            : null;

        if (realLeaf == null)
            throw new InvalidOperationException(
                "O segmento Cube.010 do modelo Market/LojaCartoon/Doors não foi encontrado.");

        // Remove only the temporary prefab introduced by the earlier setup.
        // The actual model under LojaCartoon is kept and animated in place.
        foreach (AutomaticDoor candidate in Object.FindObjectsByType<AutomaticDoor>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate.gameObject.scene != scene) continue;

            GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(candidate.gameObject);
            string prefabPath = root != null
                ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root)
                : string.Empty;
            if (candidate.name != "DoorAutomatic — Entrada" && prefabPath != DoorPrefab) continue;

            Object.DestroyImmediate(root != null ? root : candidate.gameObject);
        }

        const string triggerName = "Automatic Door Trigger — Entrada";
        Transform triggerTransform = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(candidate => candidate.name == triggerName);

        GameObject instance;
        if (triggerTransform != null)
        {
            instance = triggerTransform.gameObject;
        }
        else
        {
            instance = new GameObject(triggerName);
            SceneManager.MoveGameObjectToScene(instance, scene);
            if (market != null) instance.transform.SetParent(market, true);
        }

        Renderer leafRenderer = realLeaf.GetComponent<Renderer>();
        if (leafRenderer == null)
            throw new InvalidOperationException("O segmento Cube.010 não possui Renderer.");

        Bounds leafBounds = leafRenderer.bounds;
        instance.transform.SetPositionAndRotation(
            new Vector3(leafBounds.center.x, leafBounds.min.y + 1.5f, leafBounds.center.z),
            Quaternion.identity);
        instance.transform.localScale = Vector3.one;

        BoxCollider trigger = instance.GetComponent<BoxCollider>();
        if (trigger == null) trigger = instance.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = Vector3.zero;
        Vector3 scale = instance.transform.lossyScale;
        trigger.size = new Vector3(
            Mathf.Max(3f, leafBounds.size.x + 0.5f) / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
            3f / Mathf.Max(0.001f, Mathf.Abs(scale.y)),
            4f / Mathf.Max(0.001f, Mathf.Abs(scale.z)));

        AutomaticDoor door = instance.GetComponent<AutomaticDoor>();
        if (door == null) door = instance.AddComponent<AutomaticDoor>();

        // Cube.010 is the existing central shutter. Lift it by its own height so
        // the opening clears fully while all side/frame segments remain fixed.
        door.ConfigureWorldDoor(realLeaf, null,
                                Vector3.up * (leafBounds.size.y + 0.2f), Vector3.zero);
        GameObjectUtility.SetStaticEditorFlags(realLeaf.gameObject, 0);

        return door;
    }

    static int EnsureComputerApps(Scene scene)
    {
        int count = 0;

        foreach (Computer computer in Object.FindObjectsByType<Computer>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (computer.gameObject.scene != scene) continue;

            SerializedProperty screenProperty = new SerializedObject(computer)
                .FindProperty("computerScreen");
            GameObject screen = screenProperty != null
                ? screenProperty.objectReferenceValue as GameObject
                : null;

            if (screen == null || screen.transform is not RectTransform)
            {
                Debug.LogWarning("[Setup] Computer sem computerScreen de UI; app host não foi adicionado.", computer);
                continue;
            }

            if (screen.GetComponent<ComputerAppHost>() == null)
                Undo.AddComponent<ComputerAppHost>(screen);

            count++;
        }

        return count;
    }

    static bool EnsureToolBar(Scene scene)
    {
        ToolWheelView existing = Object.FindFirstObjectByType<ToolWheelView>(FindObjectsInactive.Include);
        if (existing != null) return true;

        PlayerInput player = Object.FindFirstObjectByType<PlayerInput>(FindObjectsInactive.Include);
        if (player == null || player.gameObject.scene != scene) return false;

        Canvas canvas = player.GetComponentsInChildren<Canvas>(true)
            .FirstOrDefault(candidate => candidate.renderMode == RenderMode.ScreenSpaceOverlay)
            ?? player.GetComponentInChildren<Canvas>(true);
        if (canvas == null) return false;

        var root = new GameObject("Tool Wheel", typeof(RectTransform));
        RectTransform rect = (RectTransform)root.transform;
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        root.AddComponent<ToolWheelView>();
        return true;
    }

    static bool RepairCursor()
    {
        if (AssetImporter.GetAtPath(CursorTexture) is not TextureImporter importer)
            return false;

        bool changed = importer.textureType != TextureImporterType.Default
                       || !importer.isReadable
                       || importer.mipmapEnabled
                       || !importer.alphaIsTransparency
                       || importer.textureCompression != TextureImporterCompression.Uncompressed;

        importer.textureType = TextureImporterType.Default;
        importer.isReadable = true;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        if (changed) importer.SaveAndReimport();
        return true;
    }

    static int RemoveMissingScripts(Scene scene)
    {
        int removed = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
            if (count == 0) continue;

            Debug.LogWarning($"[Setup] Removendo {count} script(s) ausente(s) de {GetPath(transform)}.",
                             transform.gameObject);
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
            removed += count;
        }

        return removed;
    }

    static Transform FindSceneTransform(Scene scene, string name)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(candidate => candidate.name.StartsWith(name, StringComparison.OrdinalIgnoreCase));
    }

    static string GetPath(Transform transform)
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
