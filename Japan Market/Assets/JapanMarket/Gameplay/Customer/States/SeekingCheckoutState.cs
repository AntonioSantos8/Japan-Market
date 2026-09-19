using JapanMarket.Core;
using JapanMarket.Domain;

namespace JapanMarket.Gameplay
{

    public sealed class SeekingCheckoutState : CustomerStateBase
    {

        private const float GiveUpAfterSeconds = 8f;

        private const float SearchIntervalSeconds = 0.5f;

        private float _lastSearchTime;

        private System.Predicate<UnityEngine.Vector3> _canReach;
        private CustomerContext _context;

        public override void Enter(CustomerContext context)
        {

            context.ReleaseStation();
            context.CheckoutLost = false;
            context.Locomotion.Halt();

            _context = context;
            if (_canReach == null) _canReach = CanReach;

            _lastSearchTime = context.CheckoutSearchTime;
            TryFindStation(context);
        }

        public override void Tick(CustomerContext context, float deltaTime)
        {
            context.CheckoutSearchTime += deltaTime;

            if (!context.StationLost) return;

            context.ReleaseStation();

            if (context.CheckoutSearchTime - _lastSearchTime < SearchIntervalSeconds) return;

            _lastSearchTime = context.CheckoutSearchTime;
            TryFindStation(context);
        }

        public override void Exit(CustomerContext context) => _context = null;

        private void TryFindStation(CustomerContext context)
        {
            if (context.Checkout != null
                && context.Checkout.TryFindBestStation(
                       context.Agent.Position, _canReach, out ICheckoutStation station))
            {
                context.Station = station;
                context.QueueIndex = station.QueueLength;
                return;
            }

            if (context.CheckoutSearchTime >= GiveUpAfterSeconds)
                context.Frustrate(CustomerLeaveReason.NoCheckout);
        }

        private bool CanReach(UnityEngine.Vector3 point) =>
            _context != null && _context.Locomotion.CanReach(point);

        public static bool FoundStation(CustomerContext context) => !context.StationLost;
    }
}
