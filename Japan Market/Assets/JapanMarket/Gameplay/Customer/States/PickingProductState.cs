using JapanMarket.Core;
using JapanMarket.Data;

namespace JapanMarket.Gameplay
{

    public sealed class PickingProductState : CustomerStateBase
    {
        private int _unitsWanted;
        private int _unitsTaken;
        private float _nextPickAt;

        public override void Enter(CustomerContext context)
        {
            _unitsTaken = 0;
            _unitsWanted = context.Profile != null ? context.Profile.RollUnitsPerShelf() : 1;

            float duration = context.Profile != null ? context.Profile.RollBrowseDuration() : 2f;
            context.WaitUntil = duration;

            _nextPickAt = duration * 0.35f;

            if (context.TargetShelfLost) return;

            HaltFacing(context, context.TargetShelf.Owner.Position);
        }

        public override void Tick(CustomerContext context, float deltaTime)
        {
            if (context.TargetShelfLost) return;
            if (_unitsTaken >= _unitsWanted) return;
            if (context.StateTime < _nextPickAt) return;

            TakeOne(context);

            float remaining = context.WaitUntil - context.StateTime;
            int left = _unitsWanted - _unitsTaken;
            _nextPickAt = context.StateTime + (left > 0 ? remaining / (left + 1) : remaining);
        }

        private void TakeOne(CustomerContext context)
        {
            if (context.BasketIsFull) { _unitsTaken = _unitsWanted; return; }

            ItemDefinition product = context.TargetShelf.CurrentProduct;
            if (product == null) { _unitsTaken = _unitsWanted; return; }

            if (!context.AcceptsPrice(product, out Money price))
            {
                context.Frustrate(CustomerLeaveReason.PricesTooHigh);
                _unitsTaken = _unitsWanted;
                return;
            }

            if (!context.TargetShelf.TryTakeOne(out ItemDefinition taken))
            {
                _unitsTaken = _unitsWanted;   
                return;
            }

            context.Basket.Add(taken, price);
            _unitsTaken++;

            context.Animation?.SetCarrying(true);
        }

        public override void Exit(CustomerContext context)
        {

            context.ClearShelfTarget();
            context.ShelvesRemaining--;
        }

        public static bool Finished(CustomerContext context) =>
            context.StateTime >= context.WaitUntil || context.TargetShelfLost;
    }
}
