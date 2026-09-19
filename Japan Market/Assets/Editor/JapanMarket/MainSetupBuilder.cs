using System;
using System.IO;
using System.Linq;
using System.Reflection;
using JapanMarket.Core;
using JapanMarket.Data;
using JapanMarket.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using NewTrashBin = JapanMarket.Gameplay.TrashBin;
using NewFurnitureInstance = JapanMarket.Gameplay.FurnitureInstance;

/// <summary>Repeatable setup from SETUP.md. Existing authored assets are reused.</summary>
public static class MainSetupBuilder
{
    const string Root = "Assets/JapanMarket/Setup";
    static ItemCatalog products;
    static FurnitureCatalog furniture;
    static TrashCatalog trash;
    static ObjectiveCatalog objectives;
    static LoanCatalog loans;
    static ToolBeltLayout belt;
    static GameObject binPrefab, floorGrime, glassGrime;

    public static void Run()
    {
        Directory.CreateDirectory(Root);
        AssetDatabase.Refresh();
        BuildAssets();
        if (!File.Exists("Assets/Scenes/Sandbox.unity"))
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            foreach(var method in new[]{"BuildEnvironment","BuildContext","BuildMarkers","BuildCheckout","BuildDelivery"})
                typeof(SandboxSceneBuilder).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
            EditorSceneManager.SaveScene(scene,"Assets/Scenes/Sandbox.unity");
        }
        else EditorSceneManager.OpenScene("Assets/Scenes/Sandbox.unity");
        ConfigureScene(true);
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        ConfigureScene(false);
        AssetDatabase.SaveAssets();
        Debug.Log("SETUP_COMPLETE");
    }

    static T[] All<T>() where T : Object => AssetDatabase.FindAssets("t:" + typeof(T).Name)
        .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<T>).Where(x => x != null).ToArray();
    public static void RepairDeliveryImports()
    {
        var paths = All<ItemDefinition>().Where(p => p.BoxPrefab != null)
            .SelectMany(p => p.BoxPrefab.GetComponentsInChildren<MeshFilter>(true))
            .Where(m => m.sharedMesh != null && !m.sharedMesh.isReadable)
            .Select(m => AssetDatabase.GetAssetPath(m.sharedMesh)).Distinct().ToArray();
        foreach (string path in paths)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) continue;
            importer.isReadable = true;
            importer.SaveAndReimport();
            Debug.Log("[Setup] Read/Write habilitado para o contorno da caixa: " + path);
        }
    }
    static T Asset<T>(string name) where T : ScriptableObject
    {
        string path = Root + "/" + name + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }
    static T Catalog<T>(string name) where T : ScriptableObject => All<T>().FirstOrDefault() ?? Asset<T>(name);
    static void Set(Object target, string field, object value)
    {
        Type type = target.GetType();
        FieldInfo found = null;
        while (type != null && found == null) { found = type.GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); type = type.BaseType; }
        if (found == null) throw new MissingFieldException(target.GetType().Name, field);
        found.SetValue(target, value);
        EditorUtility.SetDirty(target);
    }
    static void Name(Object asset, string field, string name) => Set(asset, field, LocalizedText.FromSingle(GameLanguage.PortugueseBrazil, name));
    static GameObject Prefab(string name, Action<GameObject> build)
    {
        string path = Root + "/" + name + ".prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;
        var go = new GameObject(name);
        try { build(go); return PrefabUtility.SaveAsPrefabAsset(go, path); }
        finally { Object.DestroyImmediate(go); }
    }
    static GameObject Shape(GameObject parent, PrimitiveType shape, Vector3 size, Color color)
    {
        var go = GameObject.CreatePrimitive(shape);
        go.name = "Visual";
        go.transform.SetParent(parent.transform, false);
        go.transform.localScale = size;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        string name = "Material_" + ColorUtility.ToHtmlStringRGB(color);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(Root + "/" + name + ".mat");
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); mat.color = color; AssetDatabase.CreateAsset(mat, Root + "/" + name + ".mat"); }
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }
    static void Carryable(GameObject go, Vector3 size)
    {
        go.AddComponent<BoxCollider>().size = size;
        go.AddComponent<Rigidbody>().mass = 0.5f;
        go.AddComponent<HoldableItem>().canBreak = false;
        go.AddComponent<Outline>();
        int layer = LayerMask.NameToLayer("Interactive");
        if (layer >= 0) go.layer = layer;
    }
    static void BuildAssets()
    {
        if (All<ItemDefinition>().Length == 0)
            typeof(LegacyItemsMigrator).GetMethod("Run", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { false });
        products = Catalog<ItemCatalog>("ItemCatalog");
        furniture = Catalog<FurnitureCatalog>("FurnitureCatalog");
        trash = Catalog<TrashCatalog>("TrashCatalog");
        objectives = Catalog<ObjectiveCatalog>("ObjectiveCatalog");
        loans = Catalog<LoanCatalog>("LoanCatalog");

        foreach (var product in All<ItemDefinition>())
        {
            if (product.BoxPrefab != null) continue;
            var box = Prefab("Caixa_" + product.name, go => {
                Shape(go, PrimitiveType.Cube, new Vector3(.5f,.35f,.4f), new Color(.6f,.4f,.2f));
                Carryable(go, new Vector3(.5f,.35f,.4f));
                var legacyBox = go.AddComponent<ItemBox>();
                Set(legacyBox, "boxType", (Items)product.LegacyEnumValue);
            });
            Set(product, "_boxPrefab", box);
        }
        string[] keys = { "plastico", "metal", "papel", "organico" };
        Color[] colors = { Color.red, Color.yellow, Color.blue, new Color(.45f,.25f,.1f) };
        for (int i = 0; i < keys.Length; i++)
        {
            var category = Asset<TrashCategory>("Categoria_" + keys[i]);
            category.EditorInitialize(keys[i], Money.FromYen(50));
            Set(category, "_color", colors[i]);
            Name(category, "_displayName", keys[i]);
            int index = i;
            var prefab = Prefab("Lixo_" + keys[i], go => {
                Shape(go, PrimitiveType.Cube, new Vector3(.15f,.15f,.2f), colors[index]);
                Carryable(go, new Vector3(.15f,.15f,.2f));
                go.AddComponent<TrashItem>();
            });
            var definition = Asset<TrashDefinition>("Lixo_" + keys[i]);
            definition.EditorInitialize(category, Money.FromYen(5), prefab, keys[i] + "_residuo");
            Name(definition, "_displayName", "Resíduo de " + keys[i]);
        }
        var bag = Prefab("SacoReciclagem", go => {
            Shape(go, PrimitiveType.Sphere, new Vector3(.45f,.6f,.45f), new Color(.12f,.15f,.12f));
            Carryable(go, new Vector3(.45f,.6f,.45f));
            go.AddComponent<TrashBagItem>();
        });
        var binDef = Asset<FurnitureDefinition>("Lixeira");
        binDef.EditorInitialize(binDef.Id.IsValid ? binDef.Id : FurnitureId.Generate());
        Name(binDef, "_displayName", "Lixeira de separação");
        Set(binDef, "_price", Money.FromYen(300));
        binPrefab = Prefab("Lixeira", go => {
            Shape(go, PrimitiveType.Cylinder, new Vector3(.65f,.45f,.65f), new Color(.2f,.35f,.25f));
            go.AddComponent<BoxCollider>().size = new Vector3(.65f,.9f,.65f);
            var trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true; trigger.center = new Vector3(0,.55f,0); trigger.size = new Vector3(.8f,.35f,.8f);
            Set(go.AddComponent<NewFurnitureInstance>(), "_definition", binDef);
            var bin = go.AddComponent<NewTrashBin>();
            Set(bin, "_bagPrefab", bag); Set(bin, "_bagCapacity", 10);
            Set(bin, "_bagSpawnPoint", Child(go, "SaidaDoSaco", new Vector3(0,.5f,.85f)));
            go.AddComponent<TrashBinInteraction>();
            go.layer = LayerMask.NameToLayer("Interactive");
        });
        Set(binDef, "_prefab", binPrefab);

        var floor = Asset<ToolSurface>("Superficie_Chao"); Name(floor,"_displayName","Chão");
        var glass = Asset<ToolSurface>("Superficie_Vidro"); Name(glass,"_displayName","Vidro");
        var counter = Asset<ToolSurface>("Superficie_Balcao"); Name(counter,"_displayName","Balcão");
        string[] names = { "Esponja", "Rodo", "Tablet", "Engradado", "Taco" };
        var slots = new ToolSlotLayout[5];
        for (int i=0; i<5; i++)
        {
            var tool = Asset<ToolDefinition>(names[i]);
            tool.EditorInitialize(i == 0 ? new[] { floor, counter } : i == 1 ? new[] { floor, glass } : new ToolSurface[0], i < 2 ? 100 : 0);
            Name(tool,"_displayName",names[i]);
            int index = i;
            Set(tool,"_heldPrefab",Prefab("Mao_" + names[i], go => Shape(go,PrimitiveType.Cube,
                index == 0 ? new Vector3(.16f,.07f,.1f) : new Vector3(.1f,.3f,.06f), index == 0 ? Color.yellow : Color.gray)));
            slots[i] = new ToolSlotLayout { Tool = tool };
        }
        belt = Catalog<ToolBeltLayout>("ToolBeltLayout"); belt.EditorSetSlots(slots); EditorUtility.SetDirty(belt);
        floorGrime = GrimePrefab("Sujeira_Chao", floor, new Vector3(.65f,.015f,.65f));
        glassGrime = GrimePrefab("Sujeira_Vidro", glass, new Vector3(.4f,.5f,.015f));

        var firstFlag = Asset<FlagUnlock>("AposPrimeiraVenda"); firstFlag.EditorSetFlag("primeira_venda"); EditorUtility.SetDirty(firstFlag);
        CreateObjective<SellItemsCondition>("PrimeiraVenda", "Primeira venda", 1, 0, null, "primeira_venda");
        CreateObjective<ServeCustomersCondition>("AtendaClientes", "Atenda cinco clientes", 5, 1, firstFlag);
        CreateObjective<ReceiveStockCondition>("RecebaEstoque", "Receba uma caixa", 1, 2);
        CreateObjective<RecycleTrashCondition>("RecicleLixo", "Separe dez resíduos", 10, 3);
        CreateObjective<SurviveDaysCondition>("PrimeiroDia", "Conclua o primeiro dia", 1, 4);
        CreateObjective<EarnRevenueCondition>("FatureVendas", "Fature mil ienes", 1000, 5, firstFlag);
        CreateObjective<ReachStoreLevelCondition>("NivelDois", "Chegue ao nível dois", 2, 6);
        for (int i=0; i<3; i++)
        {
            string key = new[] {"inicial","medio","alto"}[i];
            var loan = Asset<LoanDefinition>("Emprestimo_" + key);
            Set(loan,"_key",key); Name(loan,"_displayName","Empréstimo " + key);
            Set(loan,"_principal",Money.FromYen(new[]{2000,5000,10000}[i]));
            Set(loan,"_dailyPayment",Money.FromYen(new[]{250,600,1200}[i])); Set(loan,"_termDays",10);
            if (i > 0) { var unlock = Asset<StoreLevelUnlock>("Nivel_" + (i*3)); unlock.EditorSetRequiredLevel(i*3); EditorUtility.SetDirty(unlock); Set(loan,"_unlock",unlock); }
        }
        products.EditorSetItems(All<ItemDefinition>().ToList()); furniture.EditorSetItems(All<FurnitureDefinition>().ToList());
        trash.EditorSetItems(All<TrashDefinition>().ToList()); objectives.EditorSetItems(All<ObjectiveDefinition>().OrderBy(x=>x.SortOrder).ToList()); loans.EditorSetItems(All<LoanDefinition>().ToList());
        foreach(var obj in new Object[]{products,furniture,trash,objectives,loans}) EditorUtility.SetDirty(obj);
        AssetDatabase.SaveAssets();
        foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[]{Root}).Select(AssetDatabase.GUIDToAssetPath))
        {
            var instance = PrefabUtility.LoadPrefabContents(path);
            try { if(instance.GetComponent<InteractableBase>() != null) { instance.layer=LayerMask.NameToLayer("Interactive"); if(instance.GetComponent<HoldableItem>() != null && instance.GetComponent<Outline>() == null) instance.AddComponent<Outline>(); PrefabUtility.SaveAsPrefabAsset(instance,path); } }
            finally { PrefabUtility.UnloadPrefabContents(instance); }
        }
    }
    static void CreateObjective<T>(string key, string title, int target, int order, UnlockCondition unlock = null, string flag = null) where T : ObjectiveCondition
    {
        var condition = Asset<T>(key + "_Condicao"); condition.EditorSetTarget(target); EditorUtility.SetDirty(condition);
        var objective = Asset<ObjectiveDefinition>(key);
        objective.EditorEnsureId();
        objective.EditorInitialize(objective.Id,new ObjectiveCondition[]{condition},new ObjectiveReward{Money=Money.FromYen(100),XP=25,Flag=flag},unlock,order);
        Name(objective,"_title",title); Name(objective,"_description",condition.Describe());
    }
    static GameObject GrimePrefab(string name, ToolSurface surface, Vector3 size) => Prefab(name, go => {
        Shape(go,PrimitiveType.Cube,size,new Color(.25f,.18f,.08f)); go.AddComponent<BoxCollider>().size=size;
        Set(go.AddComponent<Grime>(),"_surface",surface);
    });
    static Transform Child(GameObject go,string name,Vector3 position)
    {
        var child = new GameObject(name).transform; child.SetParent(go.transform,false); child.localPosition=position; return child;
    }
    static T SceneComponent<T>(string name) where T:Component
    {
        var component = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
        return component != null ? component : new GameObject(name).AddComponent<T>();
    }
    static void ConfigureScene(bool sandbox)
    {
        var context = SceneComponent<GameContext>("— Game Context —");
        foreach(var pair in new[]{("_itemCatalog",(Object)products),("_furnitureCatalog",furniture),("_trashCatalog",trash),("_objectiveCatalog",objectives),("_loanCatalog",loans),("_toolBelt",belt)}) Set(context,pair.Item1,pair.Item2);
        if (!context.GetComponent<GameClockRunner>()) context.gameObject.AddComponent<GameClockRunner>();
        Set(context,"_saveOnDayEnd",true); Set(context,"_loadOnStart",false);
        var player=Object.FindFirstObjectByType<PlayerInput>(FindObjectsInactive.Include);
        Vector3 origin = sandbox ? new Vector3(-6,0,6) : new Vector3(25,.1f,5);
        var delivery=SceneComponent<DeliverySpawner>("— Depósito —");
        if (new SerializedObject(delivery).FindProperty("_dropPoint").objectReferenceValue == null)
        { delivery.transform.position=origin+Vector3.up; Set(delivery,"_dropPoint",Child(delivery.gameObject,"Drop Point",Vector3.zero)); }
        Set(delivery,"_maxBoxesOnFloor",30);
        var dock=SceneComponent<TrashDock>("— Doca de Reciclagem —");
        if (!dock.GetComponent<Collider>()) { dock.transform.position=origin+new Vector3(2,.6f,0); var c=dock.gameObject.AddComponent<BoxCollider>(); c.isTrigger=true; c.size=new Vector3(1.5f,1.2f,1.5f); Shape(dock.gameObject,PrimitiveType.Cube,new Vector3(1.5f,.05f,1.5f),Color.green).transform.localPosition=new Vector3(0,-.55f,0); }
        SceneComponent<TrashSpawner>("— Lixo —");
        if (!sandbox)
            foreach (var legacy in Object.FindObjectsByType<TrashSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None)) legacy.enabled = false;
        if (!Object.FindFirstObjectByType<NewTrashBin>()) ((GameObject)PrefabUtility.InstantiatePrefab(binPrefab)).transform.position=origin+new Vector3(-2,.45f,0);
        if (!Object.FindFirstObjectByType<Grime>()) {
            ((GameObject)PrefabUtility.InstantiatePrefab(floorGrime)).transform.position=origin+new Vector3(0,.02f,-2);
            ((GameObject)PrefabUtility.InstantiatePrefab(glassGrime)).transform.position=origin+new Vector3(-2,1.2f,-2);
        }
        if (player != null)
        {
            var tool=player.GetComponent<ToolUser>() ?? player.gameObject.AddComponent<ToolUser>();
            var camera=player.GetComponentInChildren<Camera>() ?? Camera.main;
            if(camera != null) { Set(tool,"_aimOrigin",camera.transform); var hand=camera.transform.Find("Tool Hand") ?? Child(camera.gameObject,"Tool Hand",new Vector3(.3f,-.25f,.5f)); Set(tool,"_hand",hand); }
            if(!player.GetComponent<SetupToolInput>()) player.gameObject.AddComponent<SetupToolInput>();
        }
        if (sandbox)
        {
            var profile = Asset<CustomerProfileData>("ClientePadrao");
            var customer = Prefab("ClienteSandbox", go => {
                Shape(go,PrimitiveType.Capsule,new Vector3(.6f,.9f,.6f),new Color(.35f,.5f,.7f)).transform.localPosition=Vector3.up*.9f;
                var agent=go.AddComponent<CustomerAgent>(); Set(agent,"_profile",profile);
            });
            var spawner=SceneComponent<CustomerSpawner>("— Customer Spawner —");
            Set(spawner,"_customerPrefabs",new[]{customer.GetComponent<CustomerAgent>()});
            Set(spawner,"_profiles",new[]{profile});
            if (!Object.FindFirstObjectByType<ProductStorage>())
            {
                var shelf=new GameObject("Prateleira Sandbox"); shelf.transform.position=new Vector3(0,0,3);
                shelf.AddComponent<NewFurnitureInstance>(); shelf.AddComponent<ProductStorage>(); shelf.AddComponent<CustomerSlots>();
                shelf.AddComponent<SandboxStock>().Product=products.All.First();
                Shape(shelf,PrimitiveType.Cube,new Vector3(2,.8f,.5f),Color.gray).transform.localPosition=new Vector3(0,.4f,0);
            }
            var surface=Object.FindFirstObjectByType<Unity.AI.Navigation.NavMeshSurface>();
            surface.BuildNavMesh();
            string path=Root+"/SandboxNavigation.asset";
            var old=AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(path);
            if(old == null) AssetDatabase.CreateAsset(surface.navMeshData,path);
            else { EditorUtility.CopySerialized(surface.navMeshData,old); surface.RemoveData(); surface.navMeshData=old; surface.AddData(); EditorUtility.SetDirty(old); }
        }
        EditorSceneManager.MarkSceneDirty(context.gameObject.scene);
        EditorSceneManager.SaveScene(context.gameObject.scene);
    }
}

