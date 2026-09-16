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
        BuildCheckout();
        BuildDelivery();

        if (!AssetDatabase.IsValidFolder(SceneFolder))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        Debug.Log("[Sandbox] Cena criada em " + ScenePath + ".\n" +
                  "Falta fazer à mão:\n" +
                  "  1. Assar a NavMesh (selecione 'Ground' → NavMeshSurface → Bake)\n" +
                  "  2. Atribuir ItemCatalog e FurnitureCatalog no GameContext\n" +
                  "  3. Arrastar o prefab do cliente e um CustomerProfileData no spawner\n" +
                  "  4. Colocar uma prateleira (FurnitureInstance + ProductStorage + CustomerSlots)\n" +
                  "\nO caixa já vem montado, com atendimento automático a cada 2 s " +
                  "(campo 'Sandbox' do CheckoutStation). Zere para atender à mão.\n" +
                  "O depósito também: as caixas compradas aparecem em '— Depósito —' " +
                  "quando o prazo vence (precisa de Box Prefab no asset do produto).\n" +
                  "O relógio roda a 0,2 h por segundo: o dia inteiro em ~90 s. " +
                  "Use o menu de contexto do GameClockRunner para abrir a loja, " +
                  "fechar, ou encerrar o dia na hora.");
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

        // O relógio precisa de alguém que empurre Time.deltaTime. Sem ele o dia
        // nunca vira, as contas nunca são cobradas e o relatório nunca fecha.
        contextObject.AddComponent<GameClockRunner>();

        Selection.activeGameObject = contextObject;
    }

    private static void BuildMarkers()
    {
        var markers = new GameObject("— Markers —");

        Transform entrance = CreateMarker(markers.transform, "Entrance", new Vector3(0f, 0f, -10f), "Entrance");
        Transform exit     = CreateMarker(markers.transform, "Exit",     new Vector3(0f, 0f, -14f), "Exit");
        Transform spawn    = CreateMarker(markers.transform, "CustomerSpawn", new Vector3(0f, 0f, -13f), null);

        BuildSpawner(spawn, entrance, exit);
    }

    /// <summary>
    /// Deixa o spawner montado e apontado para os três pontos. Falta só arrastar
    /// o prefab do cliente e um CustomerProfileData — o resto já está ligado.
    /// </summary>
    private static void BuildSpawner(Transform spawn, Transform entrance, Transform exit)
    {
        var host = new GameObject("— Customer Spawner —");
        CustomerSpawner spawner = host.AddComponent<CustomerSpawner>();

        var serialized = new SerializedObject(spawner);
        serialized.FindProperty("_spawnPoint").objectReferenceValue = spawn;
        serialized.FindProperty("_entryPoint").objectReferenceValue = entrance;
        serialized.FindProperty("_exitPoint").objectReferenceValue = exit;

        // Na Sandbox não existe placa de loja para abrir: começa spawnando.
        serialized.FindProperty("_autoStart").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Um caixa completo: móvel + capacidade de checkout + pontos da fila.
    ///
    /// Vem montado porque sem ele o ciclo da loja não fecha — o cliente compra,
    /// procura caixa, não acha e vai embora reclamando. Com o atendimento
    /// automático ligado, dá para apertar Play e ver a volta inteira sem ter
    /// construído nenhuma interface.
    /// </summary>
    private static void BuildCheckout()
    {
        // Fica no lado +x, com a fila crescendo para -z. Assim a cauda de oito
        // lugares (1,5 → 9,2 m da âncora) cabe folgada dentro do chão de 30 m e
        // não atravessa a porta, que está em x = 0.
        GameObject counter = GameObject.CreatePrimitive(PrimitiveType.Cube);
        counter.name = "Caixa 01";
        counter.transform.position = new Vector3(6f, 0.5f, 0f);
        counter.transform.localScale = new Vector3(2f, 1f, 1f);
        counter.isStatic = true;

        counter.AddComponent<FurnitureInstance>();
        CheckoutStation station = counter.AddComponent<CheckoutStation>();

        // Os pontos ficam FORA do cubo, senão herdam a escala 2 × 1 × 1 dele e a
        // fila sai torta. Um objeto vazio irmão é o equivalente ao que um prefab
        // de caixa de verdade teria como filho sem escala.
        var points = new GameObject("Caixa 01 · Pontos");
        points.transform.position = counter.transform.position;

        Transform queueStart = CreateChild(points.transform, "QueueAnchor",
                                           new Vector3(6f, 0f, -1.5f));
        Transform queueEnd   = CreateChild(points.transform, "QueueDirection",
                                           new Vector3(6f, 0f, -4f));
        Transform counterTop = CreateChild(points.transform, "Counter",
                                           new Vector3(6f, 1f, 0f));

        var serialized = new SerializedObject(station);
        serialized.FindProperty("_queueAnchor").objectReferenceValue = queueStart;
        serialized.FindProperty("_queueDirectionMarker").objectReferenceValue = queueEnd;
        serialized.FindProperty("_counterPoint").objectReferenceValue = counterTop;
        serialized.FindProperty("_autoServeSeconds").floatValue = 2f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// A porta dos fundos: onde a mercadoria já paga aparece.
    ///
    /// Sem este objeto na cena, o ciclo de compra não fecha — o dinheiro sai, o
    /// pedido entra na fila de entrega e a fila só enche, porque ninguém
    /// materializa a caixa. Uma Sandbox em que comprar não entrega nada é uma
    /// Sandbox que esconde exatamente o defeito que ela existe para revelar.
    /// </summary>
    private static void BuildDelivery()
    {
        // Lado -x, oposto ao caixa, para as caixas não caírem em cima da fila.
        var dock = new GameObject("— Depósito —");
        dock.transform.position = new Vector3(-8f, 0f, 6f);

        DeliverySpawner spawner = dock.AddComponent<DeliverySpawner>();

        Transform drop = CreateChild(dock.transform, "DropPoint",
                                     new Vector3(-8f, 0.5f, 6f));

        var serialized = new SerializedObject(spawner);
        serialized.FindProperty("_dropPoint").objectReferenceValue = drop;
        serialized.FindProperty("_logDeliveries").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform CreateChild(Transform parent, string name, Vector3 position)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent);
        child.transform.position = position;
        return child.transform;
    }

    private static Transform CreateMarker(Transform parent, string name, Vector3 position, string tag)
    {
        var marker = new GameObject(name);
        marker.transform.SetParent(parent);
        marker.transform.position = position;

        if (string.IsNullOrEmpty(tag)) return marker.transform;

        // As tags "Entrance" e "Exit" já existem no projeto (o código legado as
        // usa), mas se alguém abrir isto num projeto limpo o SetTag lança.
        try { marker.tag = tag; }
        catch (UnityException)
        {
            Debug.LogWarning($"[Sandbox] Tag '{tag}' não existe no projeto. " +
                             $"Crie em Project Settings → Tags and Layers e recrie a cena.");
        }

        return marker.transform;
    }
}
