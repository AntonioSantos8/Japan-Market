using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Gameplay
{

    public sealed class QueueingState : CustomerStateBase
    {
        private const float RepositionThresholdSqr = 0.04f;   

        private Vector3 _slotPosition;

        public override void Enter(CustomerContext context)
        {
            context.SaleFinished = false;
            context.QueueSettled = false;

            if (context.StationLost) return;

            if (!context.Station.TryJoinQueue(context.Agent, out int index))
            {
                context.CheckoutLost = true;
                return;
            }

            context.CheckoutSearchTime = 0f;

            context.QueueIndex = index;
            _slotPosition = context.Station.GetQueuePosition(index);

            if (!context.Locomotion.MoveTo(_slotPosition))
            {

                context.CheckoutLost = true;
            }
        }

        public override void Tick(CustomerContext context, float deltaTime)
        {
            if (context.StationLost) return;

            if (context.Locomotion.PathFailed)
            {
                context.CheckoutLost = true;
                return;
            }

            int index = context.Station.GetQueueIndex(context.Agent);
            if (index < 0)
            {

                context.CheckoutLost = true;
                return;
            }

            context.QueueIndex = index;

            Vector3 target = context.Station.GetQueuePosition(index);

            if ((target - _slotPosition).sqrMagnitude > RepositionThresholdSqr)
            {

                _slotPosition = target;
                context.QueueSettled = false;
                context.Locomotion.MoveTo(target);
                return;
            }

            if (OutOfPatience(context)) context.Frustrate(CustomerLeaveReason.WaitedTooLong);

            if (context.QueueSettled) return;

            if (context.Locomotion.IsHalted || !context.Locomotion.HasArrived) return;

            context.QueueSettled = true;
            HaltFacing(context, context.Station.CounterPosition);
        }

        public static bool MyTurn(CustomerContext context) =>
            context.QueueSettled
            && !context.StationLost
            && context.Station.IsFront(context.Agent);

        public static bool LostStation(CustomerContext context) =>
            context.StationLost || context.CheckoutLost;

        public static bool OutOfPatience(CustomerContext context)
        {
            if (context.Profile == null) return false;

            float patience = context.Profile.QueuePatience;
            return patience > 0f && context.StateTime >= patience;
        }
    }
}
