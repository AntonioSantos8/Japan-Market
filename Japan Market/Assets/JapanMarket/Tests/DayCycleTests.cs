using JapanMarket.Core;
using JapanMarket.Domain;
using NUnit.Framework;

namespace JapanMarket.Tests
{

    public sealed class DayCycleTests
    {
        private static Money Y(long yen) => Money.FromYen(yen);

        private EventBus _events;
        private GameClock _clock;
        private Ledger _ledger;
        private ExpenseService _expenses;
        private DailyReportService _reports;
        private SalesAccountant _accountant;
        private DayCycle _cycle;

        [SetUp]
        public void SetUp()
        {
            _events = new EventBus();
            _clock = new GameClock(_events, new GameClockSettings
            {
                DayStartHour = 6f, ClosingHour = 22f, EndOfDayHour = 24f,
                GameHoursPerRealSecond = 1f,
            });

            _ledger = new Ledger(_events, _clock, Y(8000));
            _expenses = new ExpenseService(_ledger);
            _reports = new DailyReportService(_events, _ledger, _clock.Day);
            _accountant = new SalesAccountant(_events, _ledger);
            _cycle = new DayCycle(_clock, _expenses, _reports, _events);

            _expenses.Register(new FlatExpense("Rent", TransactionReason.Rent, Y(100)));
        }

        [TearDown]
        public void TearDown()
        {
            _cycle.Dispose();
            _accountant.Dispose();
            _reports.Dispose();
            _ledger.Dispose();
        }

        private void Sell(long revenue, long cost, int items)
        {
            _events.Publish(new SaleCompleted(
                customerId: 1, stationId: default,
                revenue: Y(revenue), cost: Y(cost),
                itemCount: items, method: PaymentMethod.Cash));
        }

        [Test]
        public void Venda_concluida_cai_no_caixa()
        {

            Sell(revenue: 445, cost: 200, items: 3);

            Assert.AreEqual(Y(8445), _ledger.Balance);
            Assert.AreEqual(TransactionReason.ProductSale, _ledger.Today[0].Reason);
        }

        [Test]
        public void Relatorio_soma_receita_custo_e_itens()
        {
            Sell(445, 200, 3);
            Sell(155, 90, 1);

            DailyReport today = _reports.Current;

            Assert.AreEqual(Y(600), today.Revenue);
            Assert.AreEqual(Y(290), today.CostOfGoods);
            Assert.AreEqual(Y(310), today.GrossProfit);
            Assert.AreEqual(4, today.ItemsSold);
            Assert.AreEqual(2, today.CustomersServed);
        }

        [Test]
        public void Relatorio_conta_por_que_a_loja_perdeu_cliente()
        {
            _events.Publish(new CustomerLeft(1, false, CustomerLeaveReason.NoCheckout));
            _events.Publish(new CustomerLeft(2, false, CustomerLeaveReason.NoCheckout));
            _events.Publish(new CustomerLeft(3, false, CustomerLeaveReason.StoreTooDirty));
            _events.Publish(new CustomerLeft(4, true, CustomerLeaveReason.Purchased));

            DailyReport today = _reports.Current;

            Assert.AreEqual(3, today.CustomersLost);
            Assert.AreEqual(2, today.LostByReason[CustomerLeaveReason.NoCheckout]);
            Assert.AreEqual(1, today.LostByReason[CustomerLeaveReason.StoreTooDirty]);
        }

        [Test]
        public void Compras_do_jogador_entram_no_relatorio_mas_venda_nao_conta_duas_vezes()
        {
            Sell(500, 300, 2);
            _ledger.TryWithdraw(Y(730), TransactionReason.StockPurchase, "Ketchup");

            DailyReport today = _reports.Current;

            Assert.AreEqual(Y(500), today.Revenue, "Revenue only comes from SaleCompleted.");
            Assert.AreEqual(Y(730), today.Purchases);
        }

        [Test]
        public void Fechar_o_dia_cobra_as_contas_antes_de_fechar_o_relatorio()
        {
            Sell(1000, 400, 5);

            _clock.RequestEndOfDay();

            DailyReport closed = _cycle.LastClosed;

            Assert.IsNotNull(closed);
            Assert.AreEqual(1, closed.Day);
            Assert.AreEqual(Y(100), closed.Expenses);
            Assert.AreEqual(Y(500), closed.NetProfit, "1000 revenue - 400 cost - 100 rent.");
            Assert.AreEqual(Y(8900), closed.ClosingBalance,
                "The closing balance must already include the rent - that is what the order guarantees.");
            Assert.AreEqual(_ledger.Balance, closed.ClosingBalance);
        }

        [Test]
        public void As_linhas_de_despesa_ficam_guardadas_no_relatorio()
        {
            _clock.RequestEndOfDay();

            Assert.AreEqual(1, _cycle.LastClosed.ExpenseLines.Count);
            Assert.AreEqual("Rent", _cycle.LastClosed.ExpenseLines[0].Label);
        }

        [Test]
        public void O_relogio_so_vira_depois_de_o_relatorio_fechar()
        {
            int dayInsideReport = -1;
            _reports.ReportClosed += r => dayInsideReport = _clock.Day;

            _clock.RequestEndOfDay();

            Assert.AreEqual(1, dayInsideReport,
                "Turning earlier would cause day 1 lines to be registered as day 2.");
            Assert.AreEqual(2, _clock.Day);
        }

        [Test]
        public void DayEnded_sai_com_o_dia_que_acabou_e_nao_com_o_proximo()
        {
            int announced = -1;
            using (_events.Subscribe<DayEnded>(e => announced = e.Day))
            {
                _clock.RequestEndOfDay();
            }

            Assert.AreEqual(1, announced);
        }

        [Test]
        public void O_dia_seguinte_comeca_do_saldo_final_do_anterior()
        {
            Sell(1000, 400, 5);
            _clock.RequestEndOfDay();

            Assert.AreEqual(2, _reports.Current.Day);
            Assert.AreEqual(Y(8900), _reports.Current.OpeningBalance);
            Assert.AreEqual(Money.Zero, _reports.Current.Revenue);
            Assert.AreEqual(0, _ledger.Today.Count);
        }

        [Test]
        public void Fechar_duas_vezes_nao_cobra_o_aluguel_duas_vezes()
        {

            _clock.RequestEndOfDay();
            _clock.RequestEndOfDay();

            Assert.AreEqual(Y(7900), _ledger.Balance);
            Assert.AreEqual(1, _reports.ClosedReports.Count);
            Assert.AreEqual(2, _clock.Day);
        }

        [Test]
        public void Pular_o_dia_de_dentro_de_DayStarted_fecha_de_verdade()
        {

            int skips = 0;
            using (_events.Subscribe<DayStarted>(_ =>
            {
                if (skips++ == 0) _clock.RequestEndOfDay();
            }))
            {
                _clock.RequestEndOfDay();
            }

            Assert.AreEqual(2, _reports.ClosedReports.Count, "Two days, two reports.");
            Assert.AreEqual(Y(7800), _ledger.Balance, "Two days, two rents.");
            Assert.AreEqual(3, _clock.Day);

            _clock.Tick(18f);
            Assert.AreEqual(3, _reports.ClosedReports.Count);
        }

        [Test]
        public void Dois_dias_seguidos_pelo_relogio_fecham_os_dois()
        {

            _clock.Tick(18f);   
            _clock.Tick(18f);   

            Assert.AreEqual(2, _reports.ClosedReports.Count);
            Assert.AreEqual(3, _clock.Day);
            Assert.AreEqual(Y(7800), _ledger.Balance);
            Assert.AreEqual(1, _reports.ClosedReports[0].Day);
            Assert.AreEqual(2, _reports.ClosedReports[1].Day);
        }

        [Test]
        public void Saida_com_motivo_desconhecido_entra_no_relatorio()
        {

            _ledger.TryWithdraw(Y(3000), TransactionReason.Unknown, "old purchase");

            Assert.AreEqual(Y(3000), _reports.Current.Purchases);
        }

        [Test]
        public void Despesa_do_dia_nao_e_contada_tambem_como_compra()
        {

            _expenses.Register(new FlatExpense(
                "License", TransactionReason.LicensePurchase, Y(500)));

            _clock.RequestEndOfDay();

            DailyReport closed = _cycle.LastClosed;

            Assert.AreEqual(Y(600), closed.Expenses, "Rent 100 + license 500.");
            Assert.AreEqual(Money.Zero, closed.Purchases);
        }

        [Test]
        public void Caixa_e_lucro_sao_numeros_diferentes_e_ambos_corretos()
        {
            Sell(1000, 400, 5);
            _ledger.TryWithdraw(Y(3000), TransactionReason.StockPurchase, "restock");

            _clock.RequestEndOfDay();
            DailyReport closed = _cycle.LastClosed;

            Assert.AreEqual(Y(500), closed.NetProfit, "1000 - 400 cost - 100 rent.");
            Assert.AreEqual(Y(-2100), closed.CashFlow, "1000 - 3000 purchase - 100 rent.");
            Assert.AreEqual(Y(5900), closed.ClosingBalance);
        }

        [Test]
        public void Pedir_o_fim_do_dia_de_dentro_do_proprio_fechamento_nao_reentra()
        {

            using (_events.Subscribe<DayEnded>(_ => _clock.RequestEndOfDay()))
            {
                _clock.RequestEndOfDay();
            }

            Assert.AreEqual(Y(7900), _ledger.Balance);
            Assert.AreEqual(1, _reports.ClosedReports.Count);
        }

        [Test]
        public void O_dia_fecha_sozinho_quando_o_relogio_chega_na_hora()
        {
            _clock.Tick(18f);   

            Assert.AreEqual(1, _reports.ClosedReports.Count);
            Assert.AreEqual(Y(7900), _ledger.Balance);
            Assert.AreEqual(2, _clock.Day);
        }

        [Test]
        public void Fechar_o_dia_fecha_a_porta()
        {
            _clock.TryOpenStore();
            _clock.RequestEndOfDay();

            Assert.IsFalse(_clock.StoreIsOpen);
        }

        [Test]
        public void Relatorio_de_um_dia_fechado_continua_consultavel()
        {
            Sell(1000, 400, 5);
            _clock.RequestEndOfDay();

            DailyReport day1 = _reports.GetReport(1);

            Assert.IsNotNull(day1);
            Assert.IsTrue(day1.IsClosed);
            Assert.AreEqual(Y(1000), day1.Revenue);
            Assert.AreEqual(_reports.Current, _reports.GetReport(2));
            Assert.IsNull(_reports.GetReport(99));
        }

        [Test]
        public void Ticket_medio_nao_divide_por_zero()
        {
            Assert.AreEqual(Money.Zero, _reports.Current.AverageTicket);

            Sell(300, 100, 2);
            Sell(500, 200, 3);

            Assert.AreEqual(Y(400), _reports.Current.AverageTicket);
        }
    }
}
