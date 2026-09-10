using System.IO;
using JapanMarket.Gameplay;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Cria a cena Sandbox: um ambiente mínimo para validar um sistema por vez.
///
/// Por que isso é a Fase 0 e não um detalhe: hoje, testar "o NPC parou de tremer
/// na fila?" custa abrir a cena principal, girar a placa, esperar o spawn e
/// atravessar o mercado. São minutos por tentativa, e uma refatoração de IA são
/// dezenas de tentativas. Na Sandbox são segundos.
///
/// A cena é gerada por código, não versionada como .unity feito à mão: assim ela
/// pode ser recriada do zero quando estragar, e evoluir junto com as fases sem
/// virar mais um arquivo binário com conflito de merge.
///
/// Menu: Japan Market → Sandbox → Criar/Recriar cena Sandbox
/// </summary>
public static class SandboxSceneBuilder
{
    private const string SceneFolder = "Assets/Scenes";
    private const string ScenePath   = SceneFolder + "/Sandbox.unity";

    [MenuItem("Japan Market/Sandbox/Criar ou recriar cena Sandbox", priority = 300)]
    public static void CreateSandbox()
    {
        if (File.Exists(ScenePath))
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "Recriar Sandbox",
                "A cena Sandbox já existe e será substituída.\n\n" +
                "Tudo o que você tiver montado nela à mão se perde.",
                "Recriar", "Cancelar");
            if (!overwrite) return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene scene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene, NewSceneMode.Single);

        BuildEnvironment();
        BuildContext();
        BuildMarkers();

        if (!AssetDatabase.IsValidFolder(SceneFolder))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        Debug.Log("[Sandbox] Cena criada em " + ScenePath +
                  ". Falta: atribuir o ItemCatalog no GameContext e assar a NavMesh " +
                  "(selecione 'Ground' → NavMeshSurface → Bake).");
    }

    // ─────────────────────────────────────────────────────────────────────────

    private static void BuildEnvironment()
    {
        var lightObject = new GameObject("Directional Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        light.shadows = LightShadows.Soft;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;
        cameraObject.AddComponent<AudioListener>();
        cameraObject.transform.SetPositionAndRotation(
            new Vector3(0f, 6f, -9f), Quaternion.Euler(28f, 0f, 0f));

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(3f, 1f, 3f);   // 30 × 30 m
        ground.isStatic = true;

        NavMeshSurface surface = ground.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;

        var walls = new GameObject("Walls");
        CreateWall(walls.transform, "North", new Vector3(0f, 1.5f, 15f),  new Vector3(30f, 3f, 0.4f));
        CreateWall(walls.transform, "South", new Vector3(0f, 1.5f, -15f), new Vector3(30f, 3f, 0.4f));
        CreateWall(walls.transform, "East",  new Vector3(15f, 1.5f, 0f),  new Vector3(0.4f, 3f, 30f));
        CreateWall(walls.transform, "West",  new Vector3(-15f, 1.5f, 0f), new Vector3(0.4f, 3f, 30f));
    }

    private static void CreateWall(Transform parent, string name, Vector3 position, Vector3 size)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent);
        wall.transform.position = position;
        wall.transform.localScale = size;
        wall.isStatic = true;
    }

    private static void BuildContext()
    {
        var contextObject = new GameObject("— Game Context —");
        contextObject.AddComponent<GameContext>();

        Selection.activeGameObject = contextObject;
    }

    private static void BuildMarkers()
    {
        var markers = new GameObject("— Markers —");

        CreateMarker(markers.transform, "Entrance", new Vector3(0f, 0f, -12f), "Entrance");
        CreateMarker(markers.transform, "Exit",     new Vector3(0f, 0f, -14f), "Exit");
        CreateMarker(markers.transform, "NpcSpawn", new Vector3(0f, 0f, -13f), null);
    }

    private static void CreateMarker(Transform parent, string name, Vector3 position, string tag)
    {
        var marker = new GameObject(name);
        marker.transform.SetParent(parent);
        marker.transform.position = position;

        if (string.IsNullOrEmpty(tag)) return;

        // As tags "Entrance" e "Exit" já existem no projeto (o código legado as
        // usa), mas se alguém abrir isto num projeto limpo o SetTag lança.
        try { marker.tag = tag; }
        catch (UnityException)
        {
            Debug.LogWarning($"[Sandbox] Tag '{tag}' não existe no projeto. " +
                             $"Crie em Project Settings → Tags and Layers e recrie a cena.");
        }
    }
}
