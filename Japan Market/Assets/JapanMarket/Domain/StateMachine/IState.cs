namespace JapanMarket.Domain
{

    public interface IState<in TContext>
    {
        void Enter(TContext context);
        void Tick(TContext context, float deltaTime);

        void Exit(TContext context);
    }
}
