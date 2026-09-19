using System;
using System.Collections.Generic;
using JapanMarket.Core;

namespace JapanMarket.Domain
{
    /// <summary>
    /// A sequência de fechamento do dia, num lugar só e numa ordem definida.
    ///
    /// Por que isto não é "cada serviço assina <c>DayEnded</c> e se vira":
    /// porque a ordem das etapas importa e a ordem de assinatura não é
    /// controlável. As contas precisam ser cobradas ANTES de o relatório fechar,
    /// senão o saldo final do dia sai errado; e o relógio só pode virar DEPOIS
    /// do relatório, senão o dia registrado nas linhas é o seguinte. Deixar isso
    /// a cargo de quem assinou primeiro é o tipo de bug que aparece meses depois,
    /// quando alguém acrescenta um assinante.
    ///
    /// A ordem é:
    ///   1. recolhe as receitas do dia (caminhão do lixo → Ledger)
    ///   2. cobra as despesas do dia   (ExpenseService → Ledger)
    ///   3. fecha o relatório          (já enxergando o saldo final)
    ///   4. anuncia DayEnded           (telas, objetivos, save)
    ///   5. vira o relógio             (que anuncia DayStarted, e o Ledger zera)
    ///
    /// A receita vem ANTES da despesa porque o relatório precisa das duas, e
    /// porque o jogador que fica no vermelho depois de reciclar precisa ver que
    /// a reciclagem entrou. Inverter só mudaria a ordem das linhas do extrato —
    /// mas deixar a receita para DEPOIS do relatório, que era a alternativa,
    /// jogaria o dinheiro do caminhão no relatório do dia seguinte.
    /// </summary>
    public sealed class DayCycle : IDisposable
    {
        private readonly IGameClock _clock;
        private readonly IExpenseService _expenses;
        private readonly DailyReportService _reports;
        private readonly IEventBus _events;
        private readonly ILedger _ledger;
        private readonly List<IDailyIncomeSource> _income = new();

        /// <summary>
        /// Quantos dias podem fechar numa única chamada antes de a gente
        /// concluir que alguém criou um laço. Só acontece se um assinante de
        /// DayStarted pedir o fim do dia sempre — que é bug de quem assinou, mas
        /// travaria a Unity sem este teto.
        /// </summary>
        private const int MaxChainedCloses = 8;

        private bool _closing;
        private bool _hasPending;
        private int _pendingDay;

        /// <summary>
        /// <paramref name="ledger"/> é opcional só para não quebrar quem já
        /// construía este objeto sem ele: sem livro-razão nenhuma receita é
        /// depositada, e o <see cref="RegisterIncome"/> vira um registro sem
        /// efeito — por isso ele avisa no console em vez de falhar calado.
        /// </summary>
        public DayCycle(IGameClock clock, IExpenseService expenses,
                        DailyReportService reports, IEventBus events,
                        ILedger ledger = null)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _expenses = expenses;
            _reports = reports;
            _events = events;
            _ledger = ledger;

            _clock.EndOfDayReached += OnEndOfDay;
        }

        /// <summary>
        /// Acrescenta uma fonte de receita ao fechamento. Idempotente: registrar
        /// a mesma fonte duas vezes pagaria duas vezes.
        /// </summary>
        public void RegisterIncome(IDailyIncomeSource source)
        {
            if (source == null || _income.Contains(source)) return;

            if (_ledger == null)
            {
                UnityEngine.Debug.LogWarning(
                    $"[DayCycle] Fonte de receita '{source.Label}' registrada num ciclo " +
                    "sem livro-razão. Ela nunca vai pagar nada.");
            }

            _income.Add(source);
        }

        public void UnregisterIncome(IDailyIncomeSource source) => _income.Remove(source);

        /// <summary>O relatório do dia que acabou de fechar. Null antes do primeiro.</summary>
        public DailyReport LastClosed { get; private set; }

        /// <summary>
        /// Reentrância: fechar o dia publica eventos, e um assinante de
        /// <c>DayStarted</c> pode legitimamente pedir o fim do dia de novo — é o
        /// "pular o dia" de uma tela de resumo.
        ///
        /// A primeira versão simplesmente ignorava a chamada reentrante, e o
        /// resultado era pior que o problema: o relógio marcava o dia novo como
        /// encerrado, ninguém encerrava, e a partir dali NENHUM dia fechava mais
        /// — o aluguel parava de ser cobrado para sempre. Aqui o pedido é
        /// enfileirado e atendido na volta do laço.
        /// </summary>
        private void OnEndOfDay(int day)
        {
            if (_closing)
            {
                _hasPending = true;
                _pendingDay = day;
                return;
            }

            _closing = true;

            try
            {
                for (int i = 0; i < MaxChainedCloses; i++)
                {
                    _hasPending = false;

                    CloseOneDay(day);

                    if (!_hasPending) return;
                    day = _pendingDay;
                }

                UnityEngine.Debug.LogError(
                    $"[DayCycle] {MaxChainedCloses} dias fecharam em sequência numa " +
                    "chamada só. Algum assinante de DayStarted está pedindo o fim do " +
                    "dia sempre — o encadeamento foi interrompido.");
            }
            finally
            {
                _closing = false;
                _hasPending = false;
            }
        }

        private void CloseOneDay(int day)
        {
            CollectIncome();

            Money expenseTotal = Money.Zero;
            IReadOnlyList<ExpenseLine> lines = null;

            if (_expenses != null)
            {
                // Apura antes de cobrar, para guardar as linhas no relatório:
                // depois da cobrança as fontes podem devolver outro valor (um
                // móvel desligado, um empréstimo quitado).
                lines = new List<ExpenseLine>(_expenses.Preview());

                // A janela avisa ao relatório que as saídas que vêm a seguir são
                // as contas do dia, já contabilizadas em Expenses. Sem ela, uma
                // despesa cairia também em Purchases e o mesmo iene apareceria
                // duas vezes na mesma tela.
                _reports?.BeginExpenseCharge();
                try { expenseTotal = _expenses.ChargeDay(); }
                finally { _reports?.EndExpenseCharge(); }
            }

            LastClosed = _reports?.Close(day + 1, lines, expenseTotal);

            _events?.Publish(new DayEnded(day));

            _clock.AdvanceDay();
        }

        /// <summary>
        /// Uma fonte que lança não pode impedir as outras de pagar, nem impedir
        /// o dia de fechar — um dia que não fecha é a loja parada para sempre,
        /// e é um preço alto demais por um bug numa fonte de receita.
        /// </summary>
        private void CollectIncome()
        {
            if (_ledger == null || _income.Count == 0) return;

            for (int i = 0; i < _income.Count; i++)
            {
                IDailyIncomeSource source = _income[i];
                if (source == null) continue;

                try
                {
                    Money amount = source.Collect();
                    if (amount.IsPositive) _ledger.Deposit(amount, source.Reason, source.Label);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError(
                        $"[DayCycle] A fonte de receita '{source.Label}' lançou durante o " +
                        $"fechamento e foi ignorada neste dia: {e}");
                }
            }
        }

        public void Dispose() => _clock.EndOfDayReached -= OnEndOfDay;
    }
}
