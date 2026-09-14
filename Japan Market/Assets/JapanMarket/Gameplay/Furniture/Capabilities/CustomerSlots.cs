using System.Collections.Generic;
using JapanMarket.Domain;
using UnityEngine;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// Pontos onde clientes param para usar este móvel.
    ///
    /// Reescrita do <c>FurnitureOccupancy</c>, cujo vazamento de reservas é um
    /// dos defeitos mais insidiosos do projeto atual: lá, liberar um slot exige
    /// recalcular a posição de todos eles a partir do transform vivo e comparar
    /// com a posição guardada, dentro de 1 cm. Mover a prateleira entre reservar
    /// e liberar faz o loop não achar nada, e o slot fica preso para sempre.
    /// Dois vazamentos e a prateleira some do mundo dos NPCs — sem uma linha no
    /// console.
    ///
    /// Aqui a reserva é um objeto com o índice. Não há busca, não há tolerância,
    /// e o móvel pode ser movido à vontade.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CustomerSlots : FurnitureCapabilityBehaviour, ICustomerSlots
    {
        [Tooltip("Posições locais onde o cliente para. Se vazio, um slot é gerado " +
                 "à frente do móvel.")]
        [SerializeField] private Transform[] _slotAnchors;

        [Tooltip("Distância à frente do móvel para o slot gerado automaticamente.")]
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

            // O móvel pode estar sendo removido enquanto um NPC ainda procura
            // onde parar. Nesse caso ele não reserva — e o estado dele decide
            // o que fazer, em vez de descobrir tarde demais.
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

        // ── posições ─────────────────────────────────────────────────────────

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

        /// <summary>Direção em que o cliente encara o móvel ao chegar no slot.</summary>
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

                // Slots que sumiram na reconfiguração invalidam suas reservas em
                // vez de deixá-las apontando para um índice que não existe mais.
                for (int i = copy; i < _reservations.Length; i++)
                    _reservations[i]?.Invalidate();
            }
            _reservations = rebuilt;
        }

        private void OnDisable()
        {
            if (_reservations == null) return;

            // O móvel está saindo. Toda reserva vira inválida agora, para que o
            // NPC que a segura descubra pelo próprio handle — sem precisar
            // consultar um objeto que pode já ter sido destruído.
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

        // ── reserva ──────────────────────────────────────────────────────────

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

            /// <summary>O móvel morreu: a reserva morre junto, sem liberar nada.</summary>
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
