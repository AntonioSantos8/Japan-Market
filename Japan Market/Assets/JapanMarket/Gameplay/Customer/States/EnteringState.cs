namespace JapanMarket.Gameplay
{

    public sealed class EnteringState : CustomerStateBase
    {
        public override void Enter(CustomerContext context)
        {
            context.WaitUntil = context.Profile != null ? context.Profile.RollEntryDelay() : 1f;
            context.ShelvesRemaining = context.Profile != null
                ? context.Profile.RollShelvesToVisit()
                : 1;

            context.Locomotion.MoveTo(context.EntryPoint);
        }

        public override void Tick(CustomerContext context, float deltaTime)
        {

            if (context.Locomotion.IsHalted) return;

            if (context.Locomotion.HasArrived || context.Locomotion.PathFailed)
                context.Locomotion.Halt();
        }

        public static bool Finished(CustomerContext context) =>
            context.Locomotion.IsHalted && context.StateTime >= context.WaitUntil;
    }
}
