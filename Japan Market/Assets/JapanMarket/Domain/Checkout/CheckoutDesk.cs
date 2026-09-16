using System;
using System.Collections.Generic;
using JapanMarket.Core;

namespace JapanMarket.Domain
{
    /// <summary>
    /// O estado de um balcão de checkout: quem está na fila, que venda está
    /// aberta, e se ele aceita gente. C# puro.
    ///
    /// Esta classe existe porque a alternativa se mostrou pior na prática. A
    /// primeira versão colocou esta lógica dentro do MonoBehaviour
    /// <c>CheckoutStation</c>, e os testes precisaram de um dublê que a
    /// reimplementasse — duas cópias da mesma regra, que divergem exatamente
    /// como duas cópias de código de produção. Pior: o caso mais importante do
    /// refatoramento ("o jogador arranca a registradora com fila e venda
    /// aberta") passava a ser testado no dublê, não no código que roda.
    ///
    /// Com o estado aqui, o MonoBehaviour vira o que devia ser: geometria da
    /// cena, ciclo de vida do Unity e nada mais.
    ///
    /// A única coisa que o balcão não sabe sozinho é se o móvel dono dele ainda
    /// está vivo e ligado — isso é Unity. Por isso recebe essa pergunta como
    /// função no construtor, em vez de uma referência ao componente.
    /// </summary>
    public sealed class CheckoutDesk
    {
        private readonly CheckoutQueue _queue = new();
        private readonly Func<bool> _isOperational;

        public CheckoutDesk(Func<bool> isOperational = null) =>
            _isOperational = isOperational ?? (() => true);

        /// <summary>Tamanho máximo da fila. 0 ou menos = sem limite.</summary>
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

        // ── fila ─────────────────────────────────────────────────────────────

        public int GetQueueIndex(ICustomer customer) => _queue.IndexOf(customer);

        public bool IsFront(ICustomer customer) => _queue.IsFront(customer);

        /// <summary>
        /// Entra na fila. Quem já está nela recebe o próprio lugar de volta em
        /// vez de um "não": o Enter de um estado pode rodar de novo sem que o
        /// cliente tenha saído.
        /// </summary>
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

        /// <summary>
        /// Sai da fila. Idempotente de propósito: é chamado por caminhos de
        /// saída que podem rodar depois de o cliente já ter sido removido —
        /// venda concluída, estação removida.
        /// </summary>
        public void LeaveQueue(ICustomer customer)
        {
            if (customer == null) return;
            if (!_queue.Remove(customer)) return;

            // Saiu quem estava sendo atendido: a venda dele não vale mais.
            if (CurrentSession != null && ReferenceEquals(CurrentSession.Customer, customer))
                CloseSession(CurrentSession, SessionCloseReason.Abandoned);
            else
                Refresh();
        }

        /// <summary>
        /// Tira da fila quem morreu sem sair direito. Devolve true se removeu
        /// alguém — o chamador decide se isso merece um aviso.
        /// </summary>
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

        // ── venda ────────────────────────────────────────────────────────────

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

        /// <summary>
        /// Encerra a venda corrente. Ignora sessões que não são a corrente, para
        /// que uma chamada atrasada não derrube a venda do cliente seguinte.
        /// </summary>
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
                    // Quem abandonou já sabe. Avisar de novo faria o cliente que
                    // só mudou de ideia se comportar como se o caixa tivesse
                    // sumido — e voltar a procurar caixa em vez de ir embora.
                    break;
            }

            Refresh();
            SessionClosed?.Invoke(this, session, reason);
        }

        // ── ciclo de vida ────────────────────────────────────────────────────

        /// <summary>
        /// O balcão saiu de operação: desligado, removido, ou a cena morrendo.
        ///
        /// Este é o caso "jogador arranca a registradora no meio do expediente".
        /// A ordem importa: a venda aberta é encerrada primeiro, para que quem
        /// estava sendo atendido receba UM aviso e não dois, e só então a fila é
        /// dispersada. Quem chama garante que o dono já responde "não estou
        /// operante" antes desta chamada.
        /// </summary>
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
