namespace JapanMarket.Gameplay
{

    public sealed class ApproachingShelfState : CustomerStateBase
    {
        private const float RetargetThresholdSqr = 0.09f;   

        private UnityEngine.Vector3 _lastDestination;

        public override void Enter(CustomerContext context)
        {
            if (context.ReservationLost) return;

            _lastDestination = context.Reservation.WorldPosition;
            context.Locomotion.MoveTo(_lastDestination);
        }

        public override void Tick(CustomerContext context, float deltaTime)
        {
            if (context.ReservationLost) return;

            UnityEngine.Vector3 current = context.Reservation.WorldPosition;

            if ((current - _lastDestination).sqrMagnitude > RetargetThresholdSqr)
            {
                _lastDestination = current;
                context.Locomotion.MoveTo(current);
            }
        }

        public static bool Arrived(CustomerContext context) =>
            !context.ReservationLost && context.Locomotion.HasArrived;

        public static bool LostTarget(CustomerContext context) =>
            context.TargetShelfLost || context.ReservationLost || context.Locomotion.PathFailed;
    }
}
