using System;
using System.Collections;
using UnityEngine;
public class TutorialManager : MonoBehaviour
{
    [SerializeField] private MascotController mascotController;

    [SerializeField] private TutorialStepData[] steps;

    [SerializeField] private bool startAutomatically = true;

    private int _currentStepIndex = -1;
    private ITutorialState _currentState;

    public MascotController MascotController => mascotController;
    public TutorialStepData CurrentStepData =>
        (_currentStepIndex >= 0 && _currentStepIndex < steps.Length) ? steps[_currentStepIndex] : null;
    public int CurrentStepIndex => _currentStepIndex;
    public int TotalSteps => steps.Length;

   
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
        _currentStepIndex = -1;
        GoToNextStep();
    }

    public void GoToNextStep()
    {
        _currentStepIndex++;

        if (_currentStepIndex >= steps.Length)
        {
            _currentState?.Exit();
            _currentState = null;
            OnTutorialCompleted?.Invoke();
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
        TutorialStepData data = CurrentStepData;
        if (data == null) return;
        if (data.completionMode != TutorialCompletionMode.WaitForGameEvent) return;
        if (data.requiredEventId != eventId) return;

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
        _currentState?.Exit();
        _currentState = null;
        _currentStepIndex = steps.Length;
        OnTutorialCompleted?.Invoke();
    }
}
