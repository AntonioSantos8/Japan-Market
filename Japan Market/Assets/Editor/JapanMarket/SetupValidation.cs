using System;
using System.IO;
using System.Linq;
using JapanMarket.Core;
using JapanMarket.Data;
using JapanMarket.Domain;
using JapanMarket.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class SetupValidation
{
    static double started;
    static bool checkedPlay;
    static bool awaitingDelivery;
    static double deliveryStarted;
    static int boxesBefore;
    static SetupValidation()
    {
        if (!SessionState.GetBool("SetupValidation.Active",false)) return;
        Hook();
    }
    static void Hook()
    {
        EditorApplication.playModeStateChanged -= Changed;
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= Log;
        EditorApplication.playModeStateChanged += Changed;
        EditorApplication.update += Tick;
        Application.logMessageReceived += Log;
        started=EditorApplication.timeSinceStartup;
    }
    public static void Run()
    {
        File.WriteAllText("Logs/setup-validation.txt", "Setup validation\n");
        SessionState.SetBool("SetupValidation.Active",true);
        SessionState.SetInt("SetupValidation.Stage",1);
        SessionState.SetInt("SetupValidation.Errors",0);
        EditorSceneManager.OpenScene("Assets/Scenes/Sandbox.unity");
        PrepareScene();
        Hook();
        EditorApplication.EnterPlaymode();
    }
    public static void RunWithImportRepair()
    {
        MainSetupBuilder.RepairDeliveryImports();
        Run();
    }
    static void PrepareScene()
    {
        var context=Object.FindFirstObjectByType<GameContext>();
        var serialized=new SerializedObject(context);
        serialized.FindProperty("_saveOnDayEnd").boolValue=false;
        serialized.FindProperty("_loadOnStart").boolValue=false;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        started=EditorApplication.timeSinceStartup;
        checkedPlay=false;
        awaitingDelivery=false;
    }
    static void Log(string text,string trace,LogType type)
    {
        if(type!=LogType.Error && type!=LogType.Exception && type!=LogType.Assert) return;
        File.AppendAllText("Logs/setup-validation.txt",type+": "+text+"\n"+trace+"\n");
        SessionState.SetInt("SetupValidation.Errors",SessionState.GetInt("SetupValidation.Errors",0)+1);
    }
    static void Require(bool condition,string text)
    {
        if(!condition) throw new Exception("SETUP CHECK FAILED: "+text);
        File.AppendAllText("Logs/setup-validation.txt","PASS "+text+"\n");
    }
    static void Tick()
    {
        if (EditorApplication.timeSinceStartup-started > 90)
        {
            File.AppendAllText("Logs/setup-validation.txt","FAIL: timeout\n");
            SessionState.SetBool("SetupValidation.Active",false);
            EditorApplication.Exit(1);
            return;
        }
        if (EditorApplication.isPlaying && awaitingDelivery && EditorApplication.timeSinceStartup-deliveryStarted>2)
        {
            awaitingDelivery=false;
            try
            {
                Require(Object.FindObjectsByType<ItemBox>(FindObjectsSortMode.None).Length>boxesBefore,"Paid box materialized in scene");
                var game=GameContext.Current;
                Require(game.Services.Resolve<ITrashService>().PendingBags==1,"Physical dock trigger accepted the bag");
                var objectives=game.Services.Resolve<IObjectiveService>();
                game.Events.Publish(new SaleCompleted(98765,default,Money.FromYen(200),Money.FromYen(50),1,PaymentMethod.Cash));
                objectives.Flush();
                Require(game.Services.Resolve<IUnlockContext>().HasFlag("primeira_venda"),"First sale rewards unlock next objective");
                var save=new SaveService(game);
                var snapshot=save.Capture();
                save.Apply(snapshot);
                Require(game.Services.Resolve<IUnlockContext>().HasFlag("primeira_venda"),"Save round trip preserves progress in memory");
                var clock=game.Services.Resolve<IGameClock>();
                Require(clock.RequestEndOfDay(),"Day closes");
                Require(game.Services.Resolve<ITrashService>().PendingBags==0,"Recycling collected at day end");
                File.AppendAllText("Logs/setup-validation.txt","SCENE PASS "+game.gameObject.scene.name+"\n");
            }
            catch(Exception e) { Log(e.ToString(),"",LogType.Exception); }
            EditorApplication.ExitPlaymode();
            return;
        }
        if(!EditorApplication.isPlaying || checkedPlay || EditorApplication.timeSinceStartup-started<5) return;
        checkedPlay=true;
        try
        {
            var game=GameContext.Current;
            Require(game!=null,"GameContext exists");
            var services=game.Services;
            var catalog=services.Resolve<IItemCatalog>();
            Require(catalog.All.Count>0 && catalog.All.All(p=>p.BoxPrefab!=null),"Product catalog and box prefabs");
            var tools=services.Resolve<IToolBelt>();
            Require(tools.Slots.Count==5 && tools.TrySelect(0),"Five tool slots and sponge selection");
            var glass=AssetDatabase.LoadAssetAtPath<ToolSurface>("Assets/JapanMarket/Setup/Superficie_Vidro.asset");
            var floor=AssetDatabase.LoadAssetAtPath<ToolSurface>("Assets/JapanMarket/Setup/Superficie_Chao.asset");
            int uses=tools.Selected.UsesLeft;
            Require(tools.TryUse(glass,out _)==ToolUseResult.WrongSurface && tools.Selected.UsesLeft==uses,"Sponge rejects glass without wear");
            Require(tools.TryUse(floor,out _)==ToolUseResult.Ok,"Sponge cleans floor");
            var bin=Object.FindFirstObjectByType<JapanMarket.Gameplay.TrashBin>();
            var definition=services.Resolve<ITrashCatalog>().All.First();
            Require(bin.TryDiscard(definition)==TrashSortResult.Ok,"Trash accepted by bin");
            Require(bin.TryReleaseBag(out var bag) && bag.GetComponent<TrashBagItem>().Bag.Count==1,"Bag releases with contents");
            Require(bag.GetComponent<HoldableItem>()!=null,"Released bag is carryable");
            var dock=Object.FindFirstObjectByType<TrashDock>();
            Require(dock!=null && dock.GetComponent<Collider>().isTrigger,"Recycling dock trigger configured");
            bag.transform.position=dock.transform.position;
            Physics.SyncTransforms();
            var market=services.Resolve<IMarketOrderService>();
            market.DeliveryHours=0;
            var cart=new MarketCart(); cart.AddBoxes(catalog.All.First(),1);
            boxesBefore=Object.FindObjectsByType<ItemBox>(FindObjectsSortMode.None).Length;
            Require(market.TryCheckout(cart,out _)==MarketOrderResult.Ok,"Stock purchase accepted");
            var objectiveCatalog=services.Resolve<IObjectiveCatalog>();
            Require(objectiveCatalog.All.Count>=7 && objectiveCatalog.All.All(o=>o.IsValid),"Seven valid persisted objectives");
            Require(services.Resolve<ILoanCatalog>().All.Count>=3,"Loan tiers registered");
            awaitingDelivery=true;
            deliveryStarted=EditorApplication.timeSinceStartup;
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                ScreenCapture.CaptureScreenshot("Logs/setup-"+game.gameObject.scene.name+".png");
        }
        catch(Exception e) { Log(e.ToString(),"",LogType.Exception); }
        if(!awaitingDelivery) EditorApplication.ExitPlaymode();
    }
    static void Changed(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode) { started=EditorApplication.timeSinceStartup; checkedPlay=false; }
        if(state!=PlayModeStateChange.EnteredEditMode) return;
        if(SessionState.GetInt("SetupValidation.Stage",1)==1)
        {
            SessionState.SetInt("SetupValidation.Stage",2);
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            PrepareScene();
            EditorApplication.EnterPlaymode();
        }
        else
        {
            SessionState.SetBool("SetupValidation.Active",false);
            int errors=SessionState.GetInt("SetupValidation.Errors",0);
            File.AppendAllText("Logs/setup-validation.txt","ERRORS "+errors+"\n");
            EditorApplication.Exit(errors==0?0:1);
        }
    }
}
