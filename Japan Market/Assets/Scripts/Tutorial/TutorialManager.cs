using System;
using System.Collections;
using UnityEngine;
public class TutorialManager : MonoBehaviour
{
    private const string FinishTutorialEventId = "FinishTutorial";
    private const string FinishedTutorialEventId = "FinishedTutorial";

    [SerializeField] private MascotController mascotController;

    [SerializeField] private TutorialStepData[] steps;

    [SerializeField] private bool startAutomatically = true;

    private int _currentStepIndex = -1;
    private ITutorialState _currentState;
    private bool _tutorialFinished;

    private static readonly Items[] BlockedItemsDuringTutorial =
    {
        Items.Freezer, Items.IceCream, Items.FrozenMeat, Items.FrozenPizza
    };

    public MascotController MascotController => mascotController;
    public TutorialStepData CurrentStepData =>
        (_currentStepIndex >= 0 && _currentStepIndex < steps.Length) ? steps[_currentStepIndex] : null;
    public int CurrentStepIndex => _currentStepIndex;
    public int TotalSteps => steps.Length;
    public bool IsTutorialActive => !_tutorialFinished && _currentStepIndex >= 0;


    public event Action<TutorialStepData, int> OnStepChanged;

    public event Action OnTutorialCompleted;
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

    public bool IsPurchaseBlocked(Items item) =>
        IsTutorialActive && Array.IndexOf(BlockedItemsDuringTutorial, item) >= 0;
    void Awake(){ServiceLocator.Register(this);}
    private void Start()
    {
        if (startAutomatically)
            StartTutorial();
    }

    private void Update()
    {
        _currentState?.Update();
    }

    public void StartTutorial()
    {
        _tutorialFinished = false;
        mascotController?.SetTutorialVisible(true);
        _currentStepIndex = -1;
        GoToNextStep();
    }

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
        _currentState?.Exit();
        _currentState = null;
        _currentStepIndex = steps.Length;
        mascotController?.SetTutorialVisible(false);
        OnTutorialCompleted?.Invoke();
    }
}
