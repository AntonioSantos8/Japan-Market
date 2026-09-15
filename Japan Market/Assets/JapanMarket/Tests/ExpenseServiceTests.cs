using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Domain;
using NUnit.Framework;

namespace JapanMarket.Tests
{
    public sealed class ExpenseServiceTests
    {
        private static Money Y(long yen) => Money.FromYen(yen);

        private EventBus _events;
        private GameClock _clock;
        private Ledger _ledger;
        private ExpenseService _expenses;

        [SetUp]
        public void SetUp()
        {
            _events = new EventBus();
            _clock = new GameClock(_events);
            _ledger = new Ledger(_events, _clock, Y(8000));
            _expenses = new ExpenseService(_ledger);
        }

        [TearDown]
        public void TearDown() => _ledger.Dispose();

        [Test]
        public void Sem_fontes_nao_cobra_nada()
        {
            Assert.AreEqual(Money.Zero, _expenses.ChargeDay());
            Assert.AreEqual(Y(8000), _ledger.Balance);
        }

        [Test]
        public void Cobra_uma_linha_por_fonte_com_o_motivo_certo()
        {
            _expenses.Register(new FlatExpense("Aluguel", TransactionReason.Rent, Y(100)));
            _expenses.Register(new FlatExpense("Salário", TransactionReason.Salary, Y(250)));

            Assert.AreEqual(Y(350), _expenses.ChargeDay());
            Assert.AreEqual(Y(7650), _ledger.Balance);
            Assert.AreEqual(2, _ledger.Today.Count);

            // Uma linha por fonte, cada uma com o próprio motivo — é isso que
            // faz o relatório separar aluguel de salário em vez de mostrar um
            // total opaco.
            Assert.AreEqual(TransactionReason.Rent, _ledger.Today[0].Reason);
            Assert.AreEqual(Y(-100), _ledger.Today[0].Amount);
            Assert.AreEqual(TransactionReason.Salary, _ledger.Today[1].Reason);
            Assert.AreEqual(Y(-250), _ledger.Today[1].Amount);
        }

        [Test]
        public void Fonte_zerada_nao_vira_linha()
        {
            _expenses.Register(new FlatExpense("Nada", TransactionReason.Rent, Money.Zero));

            _expenses.ChargeDay();

            Assert.AreEqual(0, _ledger.Today.Count,
                "Linha de ¥0 só suja o relatório do dia.");
        }

        [Test]
        public void Registrar_a_mesma_fonte_duas_vezes_nao_cobra_duas_vezes()
        {
            var rent = new FlatExpense("Aluguel", TransactionReason.Rent, Y(100));
            _expenses.Register(rent);
            _expenses.Register(rent);

            Assert.AreEqual(Y(100), _expenses.ChargeDay());
        }

        [Test]
        public void Preview_apura_sem_cobrar()
        {
            _expenses.Register(new FlatExpense("Aluguel", TransactionReason.Rent, Y(100)));

            Assert.AreEqual(Y(100), _expenses.PreviewTotal());
            Assert.AreEqual(Y(8000), _ledger.Balance,
                "A tela de fim de dia não pode cobrar só de ser aberta.");
        }

        [Test]
        public void Despesa_de_valor_variavel_reflete_o_estado_atual()
        {
            long rent = 100;
            _expenses.Register(new FlatExpense("Aluguel", TransactionReason.Rent, () => Y(rent)));

            Assert.AreEqual(Y(100), _expenses.PreviewTotal());

            rent = 400;   // o jogador expandiu a loja
            Assert.AreEqual(Y(400), _expenses.PreviewTotal());
        }

        [Test]
        public void Conta_de_luz_e_a_soma_dos_moveis_ligados()
        {
            // O ponto do modelo de capacidades: ninguém somou nada à mão. A
            // conta é a consulta ao registro.
            var registry = new FurnitureRegistry();
            var freezer = new FakePowerConsumer(60);
            var geladeira = new FakePowerConsumer(40);

            registry.Register(new FakeFurniture().With<IPowerConsumer>(freezer));
            registry.Register(new FakeFurniture().With<IPowerConsumer>(geladeira));

            var power = new PowerExpense(registry);

            Assert.AreEqual(Y(100), power.GetDailyCost());
            Assert.AreEqual(2, power.PoweredDeviceCount);
        }

        [Test]
        public void Movel_desligado_sai_da_conta()
        {
            var registry = new FurnitureRegistry();
            var freezer = new FakePowerConsumer(60);
            registry.Register(new FakeFurniture().With<IPowerConsumer>(freezer));

            var power = new PowerExpense(registry);
            freezer.SetPowered(false);

            Assert.AreEqual(Money.Zero, power.GetDailyCost());
            Assert.AreEqual(0, power.PoweredDeviceCount);
        }

        [Test]
        public void Movel_removido_sai_da_conta_no_mesmo_dia()
        {
            var registry = new FurnitureRegistry();
            var furniture = new FakeFurniture().With<IPowerConsumer>(new FakePowerConsumer(60));
            registry.Register(furniture);

            var power = new PowerExpense(registry);
            Assert.AreEqual(Y(60), power.GetDailyCost());

            registry.Unregister(furniture);
            Assert.AreEqual(Money.Zero, power.GetDailyCost());
        }

        [Test]
        public void Movel_morto_mas_ainda_registrado_nao_entra_na_conta()
        {
            // A janela de um frame entre destruir o objeto e o registro reagir.
            var registry = new FurnitureRegistry();
            var furniture = new FakeFurniture().With<IPowerConsumer>(new FakePowerConsumer(60));
            registry.Register(furniture);

            furniture.IsAlive = false;

            Assert.AreEqual(Money.Zero, new PowerExpense(registry).GetDailyCost());
        }

        [Test]
        public void Loja_sem_dinheiro_paga_as_contas_e_fica_negativa()
        {
            var poor = new Ledger(_events, _clock, Y(50));
            var expenses = new ExpenseService(poor);
            expenses.Register(new FlatExpense("Aluguel", TransactionReason.Rent, Y(100)));

            expenses.ChargeDay();

            Assert.AreEqual(Y(-50), poor.Balance,
                "Conta chega com ou sem saldo — é assim que o jogador precisa do banco.");
            poor.Dispose();
        }

        [Test]
        public void Fonte_removida_para_de_cobrar()
        {
            var rent = new FlatExpense("Aluguel", TransactionReason.Rent, Y(100));
            _expenses.Register(rent);
            _expenses.Unregister(rent);

            Assert.AreEqual(Money.Zero, _expenses.ChargeDay());
        }

        [Test]
        public void Preview_devolve_as_linhas_com_rotulo()
        {
            _expenses.Register(new FlatExpense("Aluguel", TransactionReason.Rent, Y(100)));

            IReadOnlyList<ExpenseLine> lines = _expenses.Preview();

            Assert.AreEqual(1, lines.Count);
            Assert.AreEqual("Aluguel", lines[0].Label);
            Assert.AreEqual(Y(100), lines[0].Amount);
        }
    }
}
