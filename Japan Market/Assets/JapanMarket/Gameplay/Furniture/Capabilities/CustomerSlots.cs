using System.Collections.Generic;
using JapanMarket.Domain;
using UnityEngine;

namespace JapanMarket.Gameplay
{

    [DisallowMultipleComponent]
    public sealed class CustomerSlots : FurnitureCapabilityBehaviour, ICustomerSlots
    {
        [Tooltip("Posições locais onde o cliente para. Se vazio, um slot é gerado " +
                 "à frente do móvel.")]
        [SerializeField] private Transform[] _slotAnchors;

        [Tooltip("Distance in front of the furniture for the auto-generated slot.")]
        [SerializeField] private float _fallbackDistance = 0.9f;

        private Reservation[] _reservations;

        public int Capacity => SlotCount;

        public int FreeCount
        {
            get
            {
                EnsureReservations();
                int free = 0;
                for (int i = 0; i < _reservations.Length; i++)
                    if (_reservations[i] == null) free++;
                return free;
            }
        }

        public bool HasFreeSlot => FreeCount > 0;

        public bool TryReserve(out ISlotReservation reservation)
        {
            EnsureReservations();

            if (Owner == null || !Owner.IsAlive || !isActiveAndEnabled)
            {
                reservation = null;
                return false;
            }

            for (int i = 0; i < _reservations.Length; i++)
            {
                if (_reservations[i] != null) continue;

                var created = new Reservation(this, i);
                _reservations[i] = created;
                reservation = created;
                return true;
            }

            reservation = null;
            return false;
        }

        private int SlotCount => _slotAnchors != null && _slotAnchors.Length > 0
            ? _slotAnchors.Length
            : 1;

        internal Vector3 GetWorldPosition(int index)
        {
            if (this == null) return Vector3.zero;

            if (_slotAnchors != null && index >= 0 && index < _slotAnchors.Length
                && _slotAnchors[index] != null)
                return _slotAnchors[index].position;

            return transform.position + transform.forward * _fallbackDistance;
        }

        internal Vector3 GetFacing(int index)
        {
            if (this == null) return Vector3.forward;

            Vector3 toFurniture = transform.position - GetWorldPosition(index);
            toFurniture.y = 0f;

            return toFurniture.sqrMagnitude > 0.0001f
                ? toFurniture.normalized
                : -transform.forward;
        }

        internal void Release(int index)
        {
            if (_reservations == null) return;
            if (index < 0 || index >= _reservations.Length) return;
            _reservations[index] = null;
        }

        private void EnsureReservations()
        {
            int count = SlotCount;
            if (_reservations != null && _reservations.Length == count) return;

            var rebuilt = new Reservation[count];
            if (_reservations != null)
            {
                int copy = Mathf.Min(_reservations.Length, count);
                for (int i = 0; i < copy; i++) rebuilt[i] = _reservations[i];

                for (int i = copy; i < _reservations.Length; i++)
                    _reservations[i]?.Invalidate();
            }
            _reservations = rebuilt;
        }

        private void OnDisable()
        {
            if (_reservations == null) return;

            for (int i = 0; i < _reservations.Length; i++)
            {
                _reservations[i]?.Invalidate();
                _reservations[i] = null;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.85f, 0.9f);
            for (int i = 0; i < SlotCount; i++)
            {
                Vector3 position = GetWorldPosition(i);
                Gizmos.DrawWireSphere(position, 0.18f);
                Gizmos.DrawRay(position, GetFacing(i) * 0.4f);
            }
        }

        private sealed class Reservation : ISlotReservation
        {
            private CustomerSlots _slots;

            public int SlotIndex { get; }

            public Reservation(CustomerSlots slots, int slotIndex)
            {
                _slots = slots;
                SlotIndex = slotIndex;
            }

            public bool IsValid => _slots != null && _slots.Owner != null && _slots.Owner.IsAlive;

            public Vector3 WorldPosition => IsValid ? _slots.GetWorldPosition(SlotIndex) : Vector3.zero;
            public Vector3 Facing => IsValid ? _slots.GetFacing(SlotIndex) : Vector3.forward;

            public void Invalidate() => _slots = null;

            public void Dispose()
            {
                if (_slots == null) return;

                _slots.Release(SlotIndex);
                _slots = null;
            }
        }
    }
}
