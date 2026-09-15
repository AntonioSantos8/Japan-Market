using JapanMarket.Core;

namespace JapanMarket.Gameplay
{

    public sealed class LeavingState : CustomerStateBase
    {
        private const float DespawnDistance = 1.5f;
        private const float GiveUpAfterSeconds = 30f;

        public override void Enter(CustomerContext context)
        {

            context.ClearShelfTarget();
            context.ReleaseStation();
            context.IsLeaving = true;

            if (!context.SaleFinished) context.ReturnBasketToShelves();

            if (context.Locomotion.MoveTo(context.ExitPoint)) return;

            Depart(context);
        }

        public override void Tick(CustomerContext context, float deltaTime)
        {
            if (context.ReadyToDespawn) return;

            float distance = UnityEngine.Vector3.Distance(
                context.Agent.transform.position, context.ExitPoint);

            if (distance <= DespawnDistance)
            {
                Depart(context);
                return;
            }

            if (context.Locomotion.PathFailed || context.StateTime >= GiveUpAfterSeconds)
                Depart(context);
        }

        private static void Depart(CustomerContext context)
        {
            if (context.ReadyToDespawn) return;   
            context.ReadyToDespawn = true;

            if (context.PendingFrustration.HasValue) return;

            CustomerLeaveReason reason =
                context.SaleFinished ? CustomerLeaveReason.Purchased
                : context.StoreClosed ? CustomerLeaveReason.StoreClosed
                : CustomerLeaveReason.NothingToBuy;

            context.Events?.Publish(new CustomerLeft(
                context.Agent != null ? context.Agent.Id : 0,
                context.SaleFinished,
                reason));
        }
    }
}
