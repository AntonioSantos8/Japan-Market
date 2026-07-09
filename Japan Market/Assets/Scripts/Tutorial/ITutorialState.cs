public interface ITutorialState
{
    void Enter();
    void Exit();
    void Complete();
    void Update();
}
public abstract class BaseTutorialState : ITutorialState
{
    protected readonly TutorialManager Manager;
    protected readonly TutorialStepData Data;
    protected bool HasCompleted;

    protected BaseTutorialState(TutorialManager manager, TutorialStepData data)
    {
        Manager = manager;
        Data = data;
    }

    public virtual void Enter()
    {
        HasCompleted = false;
    }

    public virtual void Exit() { }

    public virtual void Update() { }

    public virtual void Complete()
    {
        if (HasCompleted) return;
        HasCompleted = true;
        Manager.OnStateCompleted(Data);
    }
}
