using System;
using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Domain;
using UnityEngine;

namespace JapanMarket.Tests
{
    /// <summary>
    /// Uma estação de checkout sem cena.
    ///
    /// Repare que ela NÃO reimplementa nada: fila, venda e estado são um
    /// <see cref="CheckoutDesk"/> de verdade, o mesmo que o
    /// <c>CheckoutStation</c> usa. O dublê só fornece o que exige Unity — a
    /// geometria — e o interruptor de operação. Foi por isso que o balcão saiu
    /// do MonoBehaviour: um dublê que reimplementa a regra testa o dublê.
    /// </summary>
    public sealed class FakeCheckoutStation : ICheckoutStation, IOwnedTestCapability
    {
        private readonly CheckoutDesk _desk;

        public FakeCheckoutStation()
        {
            _desk = new CheckoutDesk(() => Open && !ShuttingDown
                                           && Owner != null && Owner.IsAlive);

            _desk.StateChanged  += _ => StateChanged?.Invoke(this);
            _desk.SessionOpened += (_, s) => SessionOpened?.Invoke(this, s);
            _desk.SessionClosed += (_, s, r) => SessionClosed?.Invoke(this, s, r);
        }

        /// <summary>O móvel dono. O <c>FakeFurniture.With</c> preenche sozinho.</summary>
        public IFurniture Owner { get; set; }

        public bool Open { get; set; } = true;
        public bool ShuttingDown { get; private set; }

        public int MaxQueueLength
        {
            get => _desk.MaxQueueLength;
            set => _desk.MaxQueueLength = value;
        }

        public CheckoutDesk Desk => _desk;

        public CheckoutStationState State => _desk.State;
        public bool IsOperational => _desk.IsOperational;
        public bool AcceptsNewCustomers => _desk.AcceptsNewCustomers;
        public int QueueLength => _desk.QueueLength;
        public CheckoutSession CurrentSession => _desk.CurrentSession;

        public Vector3 QueueAnchor { get; set; } = Vector3.zero;
        public Vector3 QueueDirection { get; set; } = Vector3.back;
        public float QueueSpacing { get; set; } = 1f;
        public Vector3 CounterPosition { get; set; } = Vector3.zero;

        public event Action<ICheckoutStation> StateChanged;
        public event Action<ICheckoutStation, CheckoutSession> SessionOpened;
        public event Action<ICheckoutStation, CheckoutSession, SessionCloseReason> SessionClosed;

        public Vector3 GetQueuePosition(int index) =>
            QueueAnchor + QueueDirection * (QueueSpacing * Math.Max(0, index));

        public int GetQueueIndex(ICustomer customer) => _desk.GetQueueIndex(customer);
        public bool IsFront(ICustomer customer) => _desk.IsFront(customer);

        public bool TryJoinQueue(ICustomer customer, out int index) =>
            _desk.TryJoinQueue(customer, out index);

        public void LeaveQueue(ICustomer customer) => _desk.LeaveQueue(customer);

        public bool TryOpenSession(ICustomer customer, IReadOnlyList<SaleLine> lines,
                                   PaymentMethod method, out CheckoutSession session) =>
            _desk.TryOpenSession(customer, lines, method, out session);

        public void CloseSession(CheckoutSession session, SessionCloseReason reason) =>
            _desk.CloseSession(session, reason);

        /// <summary>Equivalente ao OnDisable do componente.</summary>
        public void Shutdown()
        {
            ShuttingDown = true;
            _desk.Shutdown();
        }
    }
}
