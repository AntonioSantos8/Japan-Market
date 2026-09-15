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
    ///   1. cobra as despesas do dia   (ExpenseService → Ledger)
    ///   2. fecha o relatório          (já enxergando o saldo final)
    ///   3. anuncia DayEnded           (telas, objetivos, save)
    ///   4. vira o relógio             (que anuncia DayStarted, e o Ledger zera)
    /// </summary>
    public sealed class DayCycle : IDisposable
    {
        private readonly IGameClock _clock;
        private readonly IExpenseService _expenses;
        private readonly DailyReportService _reports;
        private readonly IEventBus _events;

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

        public DayCycle(IGameClock clock, IExpenseService expenses,
                        DailyReportService reports, IEventBus events)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _expenses = expenses;
            _reports = reports;
            _events = events;

            _clock.EndOfDayReached += OnEndOfDay;
        }

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

        public void Dispose() => _clock.EndOfDayReached -= OnEndOfDay;
    }
}
