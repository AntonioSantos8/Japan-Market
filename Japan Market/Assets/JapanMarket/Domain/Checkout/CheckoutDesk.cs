using System;
using System.Collections.Generic;
using JapanMarket.Core;

namespace JapanMarket.Domain
{

    public sealed class CheckoutDesk
    {
        private readonly CheckoutQueue _queue = new();
        private readonly Func<bool> _isOperational;

        public CheckoutDesk(Func<bool> isOperational = null) =>
            _isOperational = isOperational ?? (() => true);

        public int MaxQueueLength { get; set; } = 8;

        public CheckoutStationState State { get; private set; } = CheckoutStationState.Idle;

        public bool IsOperational => _isOperational();

        public bool AcceptsNewCustomers =>
            IsOperational && (MaxQueueLength <= 0 || _queue.Count < MaxQueueLength);

        public int QueueLength => _queue.Count;

        public CheckoutSession CurrentSession { get; private set; }

        public event Action<CheckoutDesk> StateChanged;
        public event Action<CheckoutDesk, CheckoutSession> SessionOpened;
        public event Action<CheckoutDesk, CheckoutSession, SessionCloseReason> SessionClosed;

        public int GetQueueIndex(ICustomer customer) => _queue.IndexOf(customer);

        public bool IsFront(ICustomer customer) => _queue.IsFront(customer);

        public bool TryJoinQueue(ICustomer customer, out int index)
        {
            index = -1;
            if (customer == null || !IsOperational) return false;

            int existing = _queue.IndexOf(customer);
            if (existing >= 0) { index = existing; return true; }

            if (MaxQueueLength > 0 && _queue.Count >= MaxQueueLength) return false;

            index = _queue.Enqueue(customer);
            Refresh();
            return index >= 0;
        }

        public void LeaveQueue(ICustomer customer)
        {
            if (customer == null) return;
            if (!_queue.Remove(customer)) return;

            if (CurrentSession != null && ReferenceEquals(CurrentSession.Customer, customer))
                CloseSession(CurrentSession, SessionCloseReason.Abandoned);
            else
                Refresh();
        }

        public bool PruneDead()
        {
            if (!_queue.PruneDead()) return false;

            if (CurrentSession != null
                && (CurrentSession.Customer == null || !CurrentSession.Customer.IsAlive))
                CloseSession(CurrentSession, SessionCloseReason.Abandoned);
            else
                Refresh();

            return true;
        }

        public bool TryOpenSession(ICustomer customer, IReadOnlyList<SaleLine> lines,
                                   PaymentMethod method, out CheckoutSession session)
        {
            session = null;

            if (CurrentSession != null) return false;
            if (customer == null || lines == null || lines.Count == 0) return false;
            if (!IsOperational) return false;
            if (!_queue.IsFront(customer)) return false;

            CurrentSession = new CheckoutSession(customer, lines, method);
            Refresh();

            session = CurrentSession;
            SessionOpened?.Invoke(this, CurrentSession);
            return true;
        }

        public void CloseSession(CheckoutSession session, SessionCloseReason reason)
        {
            if (session == null || !ReferenceEquals(session, CurrentSession)) return;

            CurrentSession = null;

            ICustomer customer = session.Customer;
            _queue.Remove(customer);

            switch (reason)
            {
                case SessionCloseReason.Completed:
                    customer?.Notify(CustomerSignal.SaleFinished);
                    break;

                case SessionCloseReason.StationLost:
                    customer?.Notify(CustomerSignal.CheckoutLost);
                    break;

                case SessionCloseReason.Abandoned:

                    break;
            }

            Refresh();
            SessionClosed?.Invoke(this, session, reason);
        }

        public void Shutdown()
        {
            if (CurrentSession != null)
                CloseSession(CurrentSession, SessionCloseReason.StationLost);

            _queue.DisbandAll();

            SetState(CheckoutStationState.Unavailable);
        }

        public void Refresh()
        {
            if (!IsOperational) { SetState(CheckoutStationState.Unavailable); return; }
            if (CurrentSession != null) { SetState(CheckoutStationState.Serving); return; }

            SetState(_queue.Count > 0
                ? CheckoutStationState.Waiting
                : CheckoutStationState.Idle);
        }

        private void SetState(CheckoutStationState next)
        {
            if (next == State) return;

            State = next;
            StateChanged?.Invoke(this);
        }
    }
}
