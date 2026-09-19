using JapanMarket.Core;

namespace JapanMarket.Gameplay
{

    public sealed class FrustratedState : CustomerStateBase
    {
        private const float ReactionSeconds = 1.6f;

        public override void Enter(CustomerContext context)
        {
            context.Locomotion.Halt();
            context.WaitUntil = ReactionSeconds;
            context.FrustrationDone = false;

            context.ClearShelfTarget();

            context.ReleaseStation();

            CustomerLeaveReason reason = context.PendingFrustration ?? CustomerLeaveReason.NothingToBuy;

            context.ReturnBasketToShelves();

            context.Events?.Publish(new CustomerLeft(
                context.Agent != null ? context.Agent.Id : 0, false, reason));
        }

        public override void Tick(CustomerContext context, float deltaTime)
        {
            if (context.StateTime >= context.WaitUntil) context.FrustrationDone = true;
        }

        public static bool ReactionOver(CustomerContext context) => context.FrustrationDone;
    }
}
