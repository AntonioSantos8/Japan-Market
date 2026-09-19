using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;
using JapanMarket.Domain;
using UnityEngine;

namespace JapanMarket.Gameplay
{

    public sealed class CustomerContext
    {

        public CustomerAgent Agent;
        public CustomerLocomotion Locomotion;
        public CustomerAnimation Animation;
        public CustomerBasket Basket;
        public CustomerProfileData Profile;

        public IFurnitureRegistry Furniture;
        public IEventBus Events;
        public IPricingService Pricing;
        public ICheckoutService Checkout;

        public IStoreCleanliness Cleanliness;

        public Vector3 EntryPoint;
        public Vector3 ExitPoint;

        public IProductStorage TargetShelf;
        public ISlotReservation Reservation;
        public ICheckoutStation Station;
        public CheckoutSession Session;

        public int QueueIndex;

        public bool QueueSettled;

        public float CheckoutSearchTime;

        public int ShelvesRemaining;

        public bool StoreClosed;
        public bool SaleFinished;
        public bool CheckoutLost;

        public CustomerLeaveReason? PendingFrustration;

        public bool FrustrationDone;
        public bool ReadyToDespawn;

        public bool IsLeaving;

        public float StateTime;

        public float WaitUntil;

        public bool StoreIsTooDirty =>
            Cleanliness != null && Profile != null
            && Cleanliness.ActiveDirtCount >= Profile.DirtTolerance;

        public bool BasketIsFull =>
            Profile != null && Basket != null && Basket.Count >= Profile.BasketCapacity;

        public bool TargetShelfLost =>
            TargetShelf == null || TargetShelf.Owner == null || !TargetShelf.Owner.IsAlive;

        public bool ReservationLost => Reservation == null || !Reservation.IsValid;

        public bool StationLost =>
            !StationAlive || !Station.IsOperational;

        public bool StationAlive =>
            Station != null && Station.Owner != null && Station.Owner.IsAlive;

        public bool AcceptsPrice(ItemDefinition product, out Money price)
        {
            price = Pricing != null ? Pricing.GetSellPrice(product) : product.MarketPrice;
            if (Profile == null) return true;

            return price <= Profile.MaxAcceptablePrice(product.MarketPrice);
        }

        public void ReleaseReservation()
        {
            Reservation?.Dispose();
            Reservation = null;
        }

        public void ClearShelfTarget()
        {
            ReleaseReservation();
            TargetShelf = null;
        }

        public void ReleaseStation()
        {
            if (Station == null) { Session = null; return; }

            if (StationAlive)
            {
                if (Session != null && !Session.IsComplete)
                    Station.CloseSession(Session, SessionCloseReason.Abandoned);

                Station.LeaveQueue(Agent);
            }

            Session = null;
            Station = null;
            QueueIndex = 0;
            QueueSettled = false;
        }

        public void ReturnBasketToShelves()
        {
            if (Basket == null || Basket.IsEmpty) return;

            while (Basket.TryTakeFirst(out CustomerBasket.Entry entry))
                ReturnOne(entry);

            if (Animation != null) Animation.SetCarrying(false);
        }

        private void ReturnOne(CustomerBasket.Entry entry)
        {
            if (Furniture == null || entry.Product == null) return;

            IReadOnlyList<IProductStorage> shelves = Furniture.WithCapability<IProductStorage>();

            for (int i = 0; i < shelves.Count; i++)
            {
                IProductStorage shelf = shelves[i];

                if (shelf?.Owner == null || !shelf.Owner.IsAlive) continue;
                if (!shelf.Accepts(entry.Product)) continue;
                if (shelf.TryPlace(entry.Product, out _)) return;
            }
        }

        public void Frustrate(CustomerLeaveReason reason)
        {
            if (PendingFrustration.HasValue) return;

            PendingFrustration = reason;
            FrustrationDone = false;
        }

    }
}
