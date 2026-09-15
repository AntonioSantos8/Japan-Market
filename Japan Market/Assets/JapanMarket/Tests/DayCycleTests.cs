using JapanMarket.Core;
using JapanMarket.Domain;
using NUnit.Framework;

namespace JapanMarket.Tests
{
    /// <summary>
    /// O fechamento do dia inteiro, montado como o GameContext monta — e é aqui
    /// que a ORDEM das etapas é fixada em código executável. Se alguém um dia
    /// trocar a ordem por engano, estes testes é que dizem por quê ela era
    /// aquela.
    /// </summary>
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

            _expenses.Register(new FlatExpense("Aluguel", TransactionReason.Rent, Y(100)));
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

        // ── a venda vira dinheiro ────────────────────────────────────────────

        [Test]
        public void Venda_concluida_cai_no_caixa()
        {
            // A seta "Checkout → Economia" do diagrama, de ponta a ponta: o
            // checkout publicou, o contador creditou, e nenhum dos dois conhece
            // o outro.
            Sell(revenue: 445, cost: 200, items: 3);

            Assert.AreEqual(Y(8445), _ledger.Balance);
            Assert.AreEqual(TransactionReason.ProductSale, _ledger.Today[0].Reason);
        }

        // ── o relatório ──────────────────────────────────────────────────────

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

            Assert.AreEqual(Y(500), today.Revenue, "Receita vem só do SaleCompleted.");
            Assert.AreEqual(Y(730), today.Purchases);
        }

        // ── a ordem do fechamento ────────────────────────────────────────────

        [Test]
        public void Fechar_o_dia_cobra_as_contas_antes_de_fechar_o_relatorio()
        {
            Sell(1000, 400, 5);

            _clock.RequestEndOfDay();

            DailyReport closed = _cycle.LastClosed;

            Assert.IsNotNull(closed);
            Assert.AreEqual(1, closed.Day);
            Assert.AreEqual(Y(100), closed.Expenses);
            Assert.AreEqual(Y(500), closed.NetProfit, "1000 de receita − 400 de custo − 100 de aluguel.");
            Assert.AreEqual(Y(8900), closed.ClosingBalance,
                "O saldo final tem que já incluir o aluguel — é isso que a ordem garante.");
            Assert.AreEqual(_ledger.Balance, closed.ClosingBalance);
        }

        [Test]
        public void As_linhas_de_despesa_ficam_guardadas_no_relatorio()
        {
            _clock.RequestEndOfDay();

            Assert.AreEqual(1, _cycle.LastClosed.ExpenseLines.Count);
            Assert.AreEqual("Aluguel", _cycle.LastClosed.ExpenseLines[0].Label);
        }

        [Test]
        public void O_relogio_so_vira_depois_de_o_relatorio_fechar()
        {
            int dayInsideReport = -1;
            _reports.ReportClosed += r => dayInsideReport = _clock.Day;

            _clock.RequestEndOfDay();

            Assert.AreEqual(1, dayInsideReport,
                "Virar antes faria as linhas do dia 1 serem registradas como dia 2.");
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
            // Este é o teste que pegou o pior defeito da fase: o AdvanceDay,
            // chamado de dentro do próprio fechamento, rearmava a trava, e o
            // segundo clique no botão "ir dormir" fechava o dia SEGUINTE às 6h
            // da manhã — segundo aluguel, segundo relatório, no mesmo frame.
            _clock.RequestEndOfDay();
            _clock.RequestEndOfDay();

            Assert.AreEqual(Y(7900), _ledger.Balance);
            Assert.AreEqual(1, _reports.ClosedReports.Count);
            Assert.AreEqual(2, _clock.Day);
        }

        [Test]
        public void Pular_o_dia_de_dentro_de_DayStarted_fecha_de_verdade()
        {
            // Uma tela de resumo com "pular o dia" assina DayStarted e pede o
            // fim do dia. A primeira versão ignorava a chamada reentrante, e o
            // estrago era silencioso e permanente: o relógio marcava o dia novo
            // como encerrado, ninguém encerrava, e a partir dali NENHUM dia
            // fechava mais — o aluguel parava de ser cobrado para sempre.
            int skips = 0;
            using (_events.Subscribe<DayStarted>(_ =>
            {
                if (skips++ == 0) _clock.RequestEndOfDay();
            }))
            {
                _clock.RequestEndOfDay();
            }

            Assert.AreEqual(2, _reports.ClosedReports.Count, "Dois dias, dois relatórios.");
            Assert.AreEqual(Y(7800), _ledger.Balance, "Dois dias, dois aluguéis.");
            Assert.AreEqual(3, _clock.Day);

            // E o relógio continua funcionando depois disso.
            _clock.Tick(18f);
            Assert.AreEqual(3, _reports.ClosedReports.Count);
        }

        [Test]
        public void Dois_dias_seguidos_pelo_relogio_fecham_os_dois()
        {
            // O caminho que o jogo realmente percorre, e que nenhum teste cobria.
            _clock.Tick(18f);   // dia 1 → 24h
            _clock.Tick(18f);   // dia 2 → 24h

            Assert.AreEqual(2, _reports.ClosedReports.Count);
            Assert.AreEqual(3, _clock.Day);
            Assert.AreEqual(Y(7800), _ledger.Balance);
            Assert.AreEqual(1, _reports.ClosedReports[0].Day);
            Assert.AreEqual(2, _reports.ClosedReports[1].Day);
        }

        [Test]
        public void Saida_com_motivo_desconhecido_entra_no_relatorio()
        {
            // O Lose_Money legado registra com TransactionReason.Unknown. Se ele
            // não entrar em Purchases, o relatório mostra um saldo final que não
            // reconcilia com nenhuma linha — e a promessa de que relatório e
            // saldo nunca discordam seria falsa.
            _ledger.TryWithdraw(Y(3000), TransactionReason.Unknown, "compra antiga");

            Assert.AreEqual(Y(3000), _reports.Current.Purchases);
        }

        [Test]
        public void Despesa_do_dia_nao_e_contada_tambem_como_compra()
        {
            // Mesmo registrando uma despesa com motivo de compra — uma licença,
            // por exemplo — o iene não pode aparecer em Expenses E em Purchases.
            _expenses.Register(new FlatExpense(
                "Licença", TransactionReason.LicensePurchase, Y(500)));

            _clock.RequestEndOfDay();

            DailyReport closed = _cycle.LastClosed;

            Assert.AreEqual(Y(600), closed.Expenses, "Aluguel 100 + licença 500.");
            Assert.AreEqual(Money.Zero, closed.Purchases);
        }

        [Test]
        public void Caixa_e_lucro_sao_numeros_diferentes_e_ambos_corretos()
        {
            Sell(1000, 400, 5);
            _ledger.TryWithdraw(Y(3000), TransactionReason.StockPurchase, "reposição");

            _clock.RequestEndOfDay();
            DailyReport closed = _cycle.LastClosed;

            Assert.AreEqual(Y(500), closed.NetProfit, "1000 − 400 de custo − 100 de aluguel.");
            Assert.AreEqual(Y(-2100), closed.CashFlow, "1000 − 3000 de compra − 100 de aluguel.");
            Assert.AreEqual(Y(5900), closed.ClosingBalance);
        }

        [Test]
        public void Pedir_o_fim_do_dia_de_dentro_do_proprio_fechamento_nao_reentra()
        {
            // Um assinante de DayEnded que abre uma tela e pede o fim do dia de
            // novo. Sem a trava, seria aluguel em dobro e relatório duplicado.
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
            _clock.Tick(18f);   // 6h → 24h

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
