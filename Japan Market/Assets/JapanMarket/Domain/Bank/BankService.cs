using System;
using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;

namespace JapanMarket.Domain
{
    /// <summary>
    /// O app Banco: contratar empréstimo, pagar parcela, quitar antes.
    ///
    /// A parcela NÃO é cobrada por este serviço. Ele registra uma
    /// <see cref="FlatExpense"/> e deixa o <c>ExpenseService</c> cobrar junto do
    /// aluguel e da luz, no fechamento do dia. É por isso que a parcela aparece
    /// como uma linha própria no relatório diário sem que o relatório conheça o
    /// banco — e é a razão de o sistema de despesas existir com fontes
    /// registráveis em vez de uma lista fixa.
    ///
    /// A contagem de parcelas acontece em <c>DayEnded</c>, que o
    /// <see cref="DayCycle"/> publica DEPOIS de cobrar. A ordem importa: contar
    /// antes faria a última parcela ser contada e nunca cobrada.
    /// </summary>
    public sealed class BankService : IBankService, IDisposable
    {
        private readonly ILedger _ledger;
        private readonly IExpenseService _expenses;
        private readonly IEventBus _events;
        private readonly IUnlockContext _unlockContext;

        private readonly List<ActiveLoan> _activeLoans = new();
        private readonly Dictionary<ActiveLoan, IExpenseSource> _loanSources = new();

        private IDisposable _dayEndedSub;

        public IReadOnlyList<ActiveLoan> ActiveLoans => _activeLoans;

        public event Action<ActiveLoan> LoanTaken;
        public event Action<ActiveLoan> LoanPaidOff;

        public BankService(ILedger ledger, IExpenseService expenses, IUnlockContext unlockContext, IEventBus events = null)
        {
            _ledger = ledger;
            _expenses = expenses;
            _unlockContext = unlockContext;
            _events = events;

            if (_events != null)
            {
                _dayEndedSub = _events.Subscribe<DayEnded>(OnDayEnded);
            }
        }

        /// <summary>Soma das parcelas diárias de tudo o que está em aberto.</summary>
        public Money DailyDebtService
        {
            get
            {
                Money total = Money.Zero;
                for (int i = 0; i < _activeLoans.Count; i++)
                    total += _activeLoans[i].DailyPayment;

                return total;
            }
        }

        /// <summary>Quanto falta pagar, somando todos os empréstimos abertos.</summary>
        public Money TotalDebt
        {
            get
            {
                Money total = Money.Zero;
                for (int i = 0; i < _activeLoans.Count; i++)
                    total += _activeLoans[i].BalanceToPayOff;

                return total;
            }
        }

        /// <summary>
        /// Quantos empréstimos podem estar abertos ao mesmo tempo.
        ///
        /// Existe porque o guard por instância de LoanDefinition não segurava
        /// nada: bastava contratar todas as faixas de uma vez para transformar o
        /// banco numa fonte infinita de dinheiro no primeiro dia.
        /// </summary>
        public int MaxConcurrentLoans { get; set; } = 1;

        public bool TryTakeLoan(LoanDefinition loan)
        {
            if (loan == null || _ledger == null) return false;

            // Contrato inválido (prazo zero, parcela zero) seria dinheiro de
            // graça: o ExpenseService filtra parcela não positiva, e prazo zero
            // quita no primeiro fechamento.
            if (!loan.IsValid) return false;
            if (!loan.IsUnlocked(_unlockContext)) return false;
            if (MaxConcurrentLoans > 0 && _activeLoans.Count >= MaxConcurrentLoans) return false;

            for (int i = 0; i < _activeLoans.Count; i++)
            {
                if (_activeLoans[i].Definition == loan) return false;
            }

            var activeLoan = new ActiveLoan(loan);
            _activeLoans.Add(activeLoan);

            _ledger.Deposit(loan.Principal, TransactionReason.LoanReceived,
                            $"Empréstimo: {loan.DisplayName.Value}");
            var source = new FlatExpense($"Parcela — {loan.DisplayName.Value}",
                                         TransactionReason.LoanInstallment, loan.DailyPayment);
            _loanSources[activeLoan] = source;
            _expenses?.Register(source);

            LoanTaken?.Invoke(activeLoan);
            return true;
        }

        public bool TryPayOffEarly(ActiveLoan loan)
        {
            if (loan == null || !_activeLoans.Contains(loan) || _ledger == null) return false;

            Money balance = loan.BalanceToPayOff;

            if (!_ledger.TryWithdraw(balance, TransactionReason.LoanInstallment, $"Quitação — {loan.Definition.DisplayName.Value}"))
            {
                return false;
            }

            FinishLoan(loan);
            return true;
        }

        /// <summary>
        /// Uma parcela a menos por dia fechado.
        ///
        /// Itera uma cópia porque FinishLoan remove da lista, e roda depois do
        /// ChargeDay (ver DayCycle): o empréstimo de N dias é cobrado
        /// exatamente N vezes — a última cobrança e a última contagem acontecem
        /// no mesmo fechamento, e aí a fonte de despesa sai do registro.
        /// </summary>
        private void OnDayEnded(DayEnded e)
        {
            ActiveLoan[] loans = _activeLoans.ToArray();

            foreach (var loan in loans)
            {
                loan.RecordPayment();
                if (loan.IsPaidOff)
                {
                    FinishLoan(loan);
                }
            }
        }

        private void FinishLoan(ActiveLoan loan)
        {
            _activeLoans.Remove(loan);

            if (_loanSources.TryGetValue(loan, out var source))
            {
                _expenses?.Unregister(source);
                _loanSources.Remove(loan);
            }

            LoanPaidOff?.Invoke(loan);
        }

        /// <summary>
        /// Restaura um save.
        ///
        /// Contrato sem definição é DESCARTADO, e não restaurado pela metade: o
        /// asset do empréstimo pode ter sido apagado do projeto entre uma versão
        /// e outra, e aí o que volta do save é um <c>Definition</c> nulo. Ler o
        /// nome dele para montar a linha de despesa derrubava o carregamento
        /// inteiro — o jogador perdia a partida por causa de um empréstimo que
        /// nem existe mais. Perdoar a dívida é o pior resultado aceitável;
        /// perder o save não é.
        /// </summary>
        public void Restore(IEnumerable<ActiveLoan> loans)
        {
            _activeLoans.Clear();
            foreach (var kvp in _loanSources)
            {
                _expenses?.Unregister(kvp.Value);
            }
            _loanSources.Clear();

            if (loans == null) return;

            foreach (var loan in loans)
            {
                if (loan == null || loan.Definition == null) continue;

                _activeLoans.Add(loan);
                var source = new FlatExpense($"Parcela — {loan.Definition.DisplayName.Value}",
                                             TransactionReason.LoanInstallment, loan.DailyPayment);
                _loanSources[loan] = source;
                _expenses?.Register(source);
            }
        }

        public void Dispose()
        {
            _dayEndedSub?.Dispose();
            _dayEndedSub = null;
        }
    }
}

