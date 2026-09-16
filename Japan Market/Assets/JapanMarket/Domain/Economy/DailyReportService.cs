using System;
using System.Collections.Generic;
using JapanMarket.Core;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Mantém o relatório do dia corrente e guarda os fechados.
    ///
    /// Ele não é chamado por ninguém do gameplay: assina os eventos que já
    /// existem e vai somando. É por isso que "quantos clientes saíram por falta
    /// de caixa hoje" não exigiu uma linha nova no NPC nem no checkout.
    /// </summary>
    public interface IDailyReportService
    {
        /// <summary>O dia em andamento. Nunca null.</summary>
        DailyReport Current { get; }

        /// <summary>Um dia já fechado, ou null se não existe.</summary>
        DailyReport GetReport(int day);

        IReadOnlyList<DailyReport> ClosedReports { get; }

        /// <summary>Um dia foi fechado. É o gatilho da tela de fim de expediente.</summary>
        event Action<DailyReport> ReportClosed;
    }

    public sealed class DailyReportService : IDailyReportService, IDisposable
    {
        private readonly IEventBus _events;
        private readonly ILedger _ledger;
        private readonly List<DailyReport> _closed = new();
        private readonly List<IDisposable> _subscriptions = new();

        private bool _chargingExpenses;

        public DailyReportService(IEventBus events, ILedger ledger, int startingDay = 1)
        {
            _events = events;
            _ledger = ledger;

            Current = new DailyReport(startingDay, ledger?.Balance ?? Money.Zero);

            if (_events == null) return;

            _subscriptions.Add(_events.Subscribe<SaleCompleted>(OnSale));
            _subscriptions.Add(_events.Subscribe<CustomerLeft>(OnCustomerLeft));
            _subscriptions.Add(_events.Subscribe<TransactionRecorded>(OnTransaction));
        }

        public DailyReport Current { get; private set; }

        public IReadOnlyList<DailyReport> ClosedReports => _closed;

        public event Action<DailyReport> ReportClosed;

        public DailyReport GetReport(int day)
        {
            if (Current != null && Current.Day == day) return Current;

            for (int i = _closed.Count - 1; i >= 0; i--)
                if (_closed[i].Day == day) return _closed[i];

            return null;
        }

        // ── acumulação ───────────────────────────────────────────────────────

        private void OnSale(SaleCompleted sale)
        {
            Current.Revenue += sale.Revenue;
            Current.CostOfGoods += sale.Cost;
            Current.ItemsSold += sale.ItemCount;
            Current.CustomersServed++;
        }

        private void OnCustomerLeft(CustomerLeft left)
        {
            // Quem comprou já foi contado na venda. Aqui só entram as perdas —
            // e o motivo, que é o que o jogador precisa ver para consertar a
            // loja.
            if (left.WasSatisfied || left.Reason == CustomerLeaveReason.Purchased) return;

            Current.CountLost(left.Reason);
        }

        /// <summary>
        /// Toda saída de dinheiro que NÃO é conta de fim de dia entra em
        /// "compras".
        ///
        /// A regra é por janela, e não por lista de motivos. A primeira versão
        /// listava os <c>TransactionReason</c> de compra, e isso errava dos dois
        /// lados: uma saída com motivo <c>Unknown</c> — que é o que o
        /// <c>Lose_Money</c> legado ainda gera — sumia do relatório enquanto
        /// mexia no saldo, e uma despesa diária registrada com motivo de compra
        /// (uma licença, por exemplo) apareceria em Expenses E em Purchases.
        /// Com a janela, nenhum iene é contado duas vezes nem some.
        /// </summary>
        private void OnTransaction(TransactionRecorded transaction)
        {
            if (_chargingExpenses) return;
            if (!transaction.Amount.IsNegative) return;

            // Entrada de dinheiro não passa por aqui: a receita de venda já veio
            // pelo SaleCompleted, e somar dos dois lados contaria duas vezes.
            Current.Purchases += -transaction.Amount;
        }

        /// <summary>
        /// Abre a janela de cobrança das contas do dia. Só o <see cref="DayCycle"/>
        /// chama, em volta do <c>ChargeDay</c>.
        /// </summary>
        public void BeginExpenseCharge() => _chargingExpenses = true;

        public void EndExpenseCharge() => _chargingExpenses = false;

        // ── fechamento ───────────────────────────────────────────────────────

        /// <summary>
        /// Fecha o dia corrente e abre o próximo. Chamado pelo
        /// <see cref="DayCycle"/> DEPOIS de as despesas serem cobradas — o
        /// relatório precisa enxergá-las no saldo final.
        /// </summary>
        public DailyReport Close(int nextDay, IReadOnlyList<ExpenseLine> expenses, Money expenseTotal)
        {
            DailyReport closing = Current;

            closing.SetExpenses(expenses, expenseTotal);
            closing.ClosingBalance = _ledger?.Balance ?? closing.OpeningBalance;
            closing.IsClosed = true;

            _closed.Add(closing);

            Current = new DailyReport(nextDay, closing.ClosingBalance);

            ReportClosed?.Invoke(closing);
            return closing;
        }

        public void Dispose()
        {
            for (int i = 0; i < _subscriptions.Count; i++) _subscriptions[i]?.Dispose();
            _subscriptions.Clear();
        }
    }
}
