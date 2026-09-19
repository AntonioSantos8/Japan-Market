using System;
using System.Collections;
using JapanMarket.Core;
using JapanMarket.Domain;
using JapanMarket.Gameplay;
using UnityEngine;
public class TutorialManager : MonoBehaviour, ITutorialStatus
{
    private const string FinishTutorialEventId = "FinishTutorial";
    private const string FinishedTutorialEventId = "FinishedTutorial";

    [SerializeField] private MascotController mascotController;

    [SerializeField] private TutorialStepData[] steps;

    [SerializeField] private bool startAutomatically = true;

    [Header("Primeiro dia do tutorial")]
    [Tooltip("Impede que o primeiro dia termine antes de clientes suficientes serem atendidos.")]
    [SerializeField] private bool protectFirstDay = true;

    [Min(1)]
    [Tooltip("Vendas completas necessárias para encerrar automaticamente o primeiro dia.")]
    [SerializeField] private int customersToEndFirstDay = 3;

    private int _currentStepIndex = -1;
    private ITutorialState _currentState;
    private bool _tutorialFinished;
    private TutorialDayGate _tutorialDayGate;
    private bool _dayGateInitialized;
    private IObjectiveService _objectives;

    public MascotController MascotController => mascotController;
    public TutorialStepData CurrentStepData =>
        (_currentStepIndex >= 0 && _currentStepIndex < steps.Length) ? steps[_currentStepIndex] : null;
    public int CurrentStepIndex => _currentStepIndex;
    public int TotalSteps => steps.Length;
    public int FirstDayCustomersServed => _tutorialDayGate?.CustomersServed ?? 0;
    public int FirstDayCustomerTarget => Mathf.Max(1, customersToEndFirstDay);
    public bool IsFirstDayProtected => _tutorialDayGate?.IsActive == true;
    public bool IsTutorialFinished => _tutorialFinished;
    public bool IsFinished => _tutorialFinished;

   
    public event Action<TutorialStepData, int> OnStepChanged;

    public event Action OnTutorialCompleted;
    public event Action Completed
    {
        add => OnTutorialCompleted += value;
        remove => OnTutorialCompleted -= value;
    }
    bool boughtFurniture, boughtFood;
    public void BoughtItem(SellingItemType sellingItemType)
    {
        if (sellingItemType == SellingItemType.Furniture)
            boughtFurniture = true;
        else if (sellingItemType == SellingItemType.Food)
            boughtFood = true;

        if(boughtFurniture && boughtFood)
            NotifyGameEvent("BoughtFurnitureAndFood");
    }
    void Awake()
    {
        ServiceLocator.Register(this);
        ServiceLocator.Register<ITutorialStatus>(this);
        ServiceContainer.Current.TryResolve(out _objectives);
        _objectives?.SetTrackingEnabled(false);
    }
    private void Start()
    {
        TryInitializeTutorialDayGate();

        if (startAutomatically)
            StartTutorial();
    }

    private void Update()
    {
        // GameContext normalmente nasce antes deste componente. A tentativa no
        // Update deixa o prefab seguro também em cenas que o instanciam depois.
        TryInitializeTutorialDayGate();
        _tutorialDayGate?.ProcessPendingEndOfDay();
        _currentState?.Update();
    }

    private void TryInitializeTutorialDayGate()
    {
        if (_dayGateInitialized) return;

        if (!protectFirstDay)
        {
            _dayGateInitialized = true;
            return;
        }

        GameContext game = GameContext.Current;
        if (game == null || game.Clock == null || game.Events == null) return;

        _dayGateInitialized = true;

        // Um save iniciado depois do primeiro dia nunca deve voltar a receber
        // as regras especiais do tutorial.
        if (game.Clock.Day != 1) return;

        _tutorialDayGate = new TutorialDayGate(
            game.Clock, game.Events, protectedDay: 1,
            requiredCustomers: FirstDayCustomerTarget);
        _tutorialDayGate.ProgressChanged += OnFirstDayProgressChanged;
    }

    private void OnFirstDayProgressChanged(int served, int target)
    {
        Debug.Log($"[Tutorial] Clientes atendidos no primeiro dia: {served}/{target}.", this);
    }

    public void StartTutorial()
    {
        _tutorialFinished = false;
        _objectives?.SetTrackingEnabled(false);
        mascotController?.SetTutorialVisible(true);
        _currentStepIndex = -1;
        GoToNextStep();
    }

    [ContextMenu("Go To Next Step")]
    public void GoToNextStep()
    {
        if (_tutorialFinished) return;

        _currentStepIndex++;

        if (_currentStepIndex >= steps.Length)
        {
            FinishTutorialInternal();
            return;
        }

        TutorialStepData data = steps[_currentStepIndex];
        ITutorialState nextState = CreateStateForStep(data);
        ChangeState(nextState);
        OnStepChanged?.Invoke(data, _currentStepIndex);
    }

    private ITutorialState CreateStateForStep(TutorialStepData data)
    {
        return new DialogueTutorialState(this, data);
    }

    private void ChangeState(ITutorialState state)
    {
        _currentState?.Exit();
        _currentState = state;
        _currentState?.Enter();
    }

    public void OnStateCompleted(TutorialStepData data)
    {
        if (data != CurrentStepData) return;
        GoToNextStep();
    }

  
    public void CompleteCurrentState()
    {
        _currentState?.Complete();
    }
    public void NotifyGameEvent(string eventId)
    {
        if (_tutorialFinished) return;

        TutorialStepData data = CurrentStepData;
        if (data == null) return;
        if (data.completionMode != TutorialCompletionMode.WaitForGameEvent) return;

        bool isFinishEventAlias = eventId == FinishTutorialEventId || eventId == FinishedTutorialEventId;
        bool stepExpectsFinishEvent = data.requiredEventId == FinishTutorialEventId || data.requiredEventId == FinishedTutorialEventId;

        if (!isFinishEventAlias || !stepExpectsFinishEvent)
        {
            if (data.requiredEventId != eventId) return;
        }

        if (isFinishEventAlias)
        {
            FinishTutorialInternal();
            return;
        }

        CompleteCurrentState();
    }
    public void StartCoroutineExternal(IEnumerator routine)
    {
        StartCoroutine(routine);
    }
    public void HandleClick()
    {
        if (_currentState is DialogueTutorialState dialogueState)
            dialogueState.HandleClick();
    }
    public void SkipTutorial()
    {
        FinishTutorialInternal();
    }

    private void FinishTutorialInternal()
    {
        if (_tutorialFinished) return;

        _tutorialFinished = true;
        _objectives?.SetTrackingEnabled(true);
        _currentState?.Exit();
        _currentState = null;
        _currentStepIndex = steps.Length;
        mascotController?.SetTutorialVisible(false);
        OnTutorialCompleted?.Invoke();
    }

    private void OnDestroy()
    {
        if (!_tutorialFinished)
            _objectives?.SetTrackingEnabled(true);

        if (_tutorialDayGate != null)
        {
            _tutorialDayGate.ProgressChanged -= OnFirstDayProgressChanged;
            _tutorialDayGate.Dispose();
            _tutorialDayGate = null;
        }

        ServiceLocator.Unregister<ITutorialStatus>();
        ServiceLocator.Unregister<TutorialManager>();
    }
}
