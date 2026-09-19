using System;
using JapanMarket.Core;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Protege o primeiro expediente do tutorial e o encerra depois que o
    /// jogador realmente atendeu uma quantidade mínima de clientes.
    ///
    /// Contamos SaleCompleted, não CustomerEntered: fechar quando o terceiro
    /// NPC cruza a porta poderia interromper os três no meio da compra e deixar
    /// o tutorial sem uma venda concluída.
    /// </summary>
    public sealed class TutorialDayGate : IDisposable
    {
        private readonly IGameClock _clock;
        private IDisposable _saleSubscription;

        public TutorialDayGate(IGameClock clock, IEventBus events,
                               int protectedDay, int requiredCustomers)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            if (events == null) throw new ArgumentNullException(nameof(events));
            ProtectedDay = Math.Max(1, protectedDay);
            RequiredCustomers = Math.Max(1, requiredCustomers);

            if (_clock.Day != ProtectedDay) return;

            IsActive = true;
            _clock.SetEndOfDayLocked(true);
            _saleSubscription = events.Subscribe<SaleCompleted>(OnSaleCompleted);
        }

        public int ProtectedDay { get; }
        public int RequiredCustomers { get; }
        public int CustomersServed { get; private set; }
        public bool IsActive { get; private set; }
        public bool IsCompletionPending { get; private set; }

        public event Action<int, int> ProgressChanged;
        public event Action Completed;

        private void OnSaleCompleted(SaleCompleted sale)
        {
            if (!IsActive) return;

            if (_clock.Day != ProtectedDay)
            {
                Release(endDay: false);
                return;
            }

            CustomersServed++;
            ProgressChanged?.Invoke(CustomersServed, RequiredCustomers);

            if (CustomersServed >= RequiredCustomers)
            {
                // SaleCompleted é publicado no meio da finalização física do
                // caixa. Encerrar o dia aqui fecharia a loja e mexeria nas
                // filas enquanto CashRegister ainda remove o cliente atual.
                // O MonoBehaviour hospedeiro conclui no frame seguinte.
                IsCompletionPending = true;
                _saleSubscription?.Dispose();
                _saleSubscription = null;
            }
        }

        /// <summary>
        /// Conclui um fechamento já solicitado por vendas. Deve ser chamado
        /// fora do despacho de <see cref="SaleCompleted"/>.
        /// </summary>
        public bool ProcessPendingEndOfDay()
        {
            if (!IsActive) return false;

            // Um save pode ser aplicado entre a criação do gate e o primeiro
            // Update. Se ele já estiver em outro dia, solte a proteção mesmo
            // sem uma venda nova.
            if (_clock.Day != ProtectedDay)
            {
                Release(endDay: false);
                return false;
            }

            if (!IsCompletionPending) return false;

            Release(endDay: true);
            return true;
        }

        private void Release(bool endDay)
        {
            if (!IsActive) return;

            IsActive = false;
            IsCompletionPending = false;
            _saleSubscription?.Dispose();
            _saleSubscription = null;
            _clock.SetEndOfDayLocked(false);

            Completed?.Invoke();
            if (endDay) _clock.RequestEndOfDay();
        }

        public void Dispose() => Release(endDay: false);
    }
}
