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
    static SetupValidation()
    {
        if (!SessionState.GetBool("SetupValidation.Active",false)) return;
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
        EditorApplication.EnterPlaymode();
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
        if(!EditorApplication.isPlaying || checkedPlay || EditorApplication.timeSinceStartup-started<5) return;
        checkedPlay=true;
        try
        {
            var game=GameContext.Current;
            Require(game!=null,"GameContext " + game.gameObject.scene.name);
            var services=game.Services;
            var catalog=services.Resolve<IItemCatalog>();
            Require(catalog.All.Count>0 && catalog.All.All(p=>p.BoxPrefab!=null),"Product catalog and box prefabs");
            var tools=services.Resolve<IToolBelt>();
            Require(tools.Slots.Count==5 && tools.TrySelect(0),"Five tool slots and sponge selection");
            var glass=AssetDatabase.LoadAssetAtPath<ToolSurface>("Assets/JapanMarket/Setup/Superficie_Vidro.asset");
            var floor=AssetDatabase.LoadAssetAtPath<ToolSurface>("Assets/JapanMarket/Setup/Superficie_Chao.asset");
            Require(tools.TryUse(glass,out _)==ToolUseResult.WrongSurface,"Sponge rejects glass");
            Require(tools.TryUse(floor,out _)==ToolUseResult.Ok,"Sponge cleans floor");
            var bin=Object.FindFirstObjectByType<JapanMarket.Gameplay.TrashBin>();
            var definition=services.Resolve<ITrashCatalog>().All.First();
            Require(bin.TryDiscard(definition)==TrashSortResult.Ok,"Trash accepted by bin");
            Require(bin.TryReleaseBag(out var bag) && bag.GetComponent<TrashBagItem>().Bag.Count==1,"Bag releases with contents");
            Require(bag.GetComponent<HoldableItem>()!=null,"Released bag is carryable");
            Require(services.Resolve<ITrashService>().TryDeposit(bag.GetComponent<TrashBagItem>().Bag),"Bag deposits in recycling service");
            var market=services.Resolve<IMarketOrderService>();
            market.DeliveryHours=0;
            var cart=new MarketCart(); cart.AddBoxes(catalog.All.First(),1);
            Require(market.TryCheckout(cart,out _)==MarketOrderResult.Ok,"Stock purchase accepted");
            var objectiveCatalog=services.Resolve<IObjectiveCatalog>();
            Require(objectiveCatalog.All.Count>=7 && objectiveCatalog.All.All(o=>o.IsValid),"Seven valid persisted objectives");
            Require(services.Resolve<ILoanCatalog>().All.Count>=3,"Loan tiers registered");
            File.AppendAllText("Logs/setup-validation.txt","SCENE PASS "+game.gameObject.scene.name+"\n");
        }
        catch(Exception e) { Log(e.ToString(),"",LogType.Exception); }
        EditorApplication.ExitPlaymode();
    }
    static void Changed(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode) { started=EditorApplication.timeSinceStartup; checkedPlay=false; }
        if(state!=PlayModeStateChange.EnteredEditMode) return;
        if(SessionState.GetInt("SetupValidation.Stage",1)==1)
        {
            SessionState.SetInt("SetupValidation.Stage",2);
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
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
