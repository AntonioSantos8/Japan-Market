using System.Collections.Generic;
using JapanMarket.Domain;

namespace JapanMarket.Gameplay
{

    public sealed class BrowsingState : CustomerStateBase
    {
        private readonly List<IProductStorage> _candidates = new();

        private const float GiveUpAfterSeconds = 5f;

        public override void Enter(CustomerContext context)
        {
            context.ClearShelfTarget();
            context.Locomotion.Halt();
        }

        public override void Tick(CustomerContext context, float deltaTime)
        {

            if (context.ShelvesRemaining <= 0 || context.BasketIsFull)
            {
                if (context.Basket.IsEmpty) GiveUpIfOutOfTime(context);
                return;
            }

            if (context.TargetShelf != null) return;

            TryPickShelf(context);
        }

        private void TryPickShelf(CustomerContext context)
        {
            _candidates.Clear();

            IReadOnlyList<IProductStorage> all = context.Furniture.WithCapability<IProductStorage>();

            for (int i = 0; i < all.Count; i++)
            {
                IProductStorage storage = all[i];

                if (storage?.Owner == null || !storage.Owner.IsAlive) continue;
                if (storage.IsEmpty) continue;
                if (!context.AcceptsPrice(storage.CurrentProduct, out _)) continue;

                if (!storage.Owner.TryGetCapability(out ICustomerSlots slots)) continue;
                if (!slots.HasFreeSlot) continue;

                _candidates.Add(storage);
            }

            if (_candidates.Count == 0)
            {

                if (context.Basket.IsEmpty) GiveUpIfOutOfTime(context);
                else context.ShelvesRemaining = 0;

                return;
            }

            IProductStorage chosen = _candidates[UnityEngine.Random.Range(0, _candidates.Count)];

            if (!chosen.Owner.TryGetCapability(out ICustomerSlots chosenSlots)) return;
            if (!chosenSlots.TryReserve(out ISlotReservation reservation)) return;

            if (!context.Locomotion.CanReach(reservation.WorldPosition))
            {
                reservation.Dispose();
                return;
            }

            context.TargetShelf = chosen;
            context.Reservation = reservation;
        }

        private static void GiveUpIfOutOfTime(CustomerContext context)
        {
            if (context.StateTime < GiveUpAfterSeconds) return;
            context.Frustrate(Core.CustomerLeaveReason.NothingToBuy);
        }

        public override void Exit(CustomerContext context) => _candidates.Clear();

        public static bool FoundShelf(CustomerContext context) =>
            context.TargetShelf != null && context.Reservation != null;

        public static bool DoneShopping(CustomerContext context) =>
            context.TargetShelf == null
            && !context.Basket.IsEmpty
            && (context.ShelvesRemaining <= 0 || context.BasketIsFull);
    }
}
