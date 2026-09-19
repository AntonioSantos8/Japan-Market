using JapanMarket.Core;
using JapanMarket.Domain;
using NUnit.Framework;

namespace JapanMarket.Tests
{
    public sealed class LedgerTests
    {
        private static Money Y(long yen) => Money.FromYen(yen);

        private EventBus _events;
        private GameClock _clock;
        private Ledger _ledger;

        [SetUp]
        public void SetUp()
        {
            _events = new EventBus();
            _clock = new GameClock(_events);
            _ledger = new Ledger(_events, _clock, Y(8000));
        }

        [TearDown]
        public void TearDown() => _ledger.Dispose();

        [Test]
        public void Comeca_com_o_saldo_de_abertura_e_sem_movimento()
        {
            Assert.AreEqual(Y(8000), _ledger.Balance);
            Assert.AreEqual(0, _ledger.Today.Count,
                "Opening balance is not a movement - otherwise day 1 is born with a phantom revenue.");
        }

        [Test]
        public void Deposito_soma_e_registra()
        {
            _ledger.Deposit(Y(445), TransactionReason.ProductSale, "3 items");

            Assert.AreEqual(Y(8445), _ledger.Balance);
            Assert.AreEqual(1, _ledger.Today.Count);
            Assert.IsTrue(_ledger.Today[0].IsIncome);
            Assert.AreEqual(TransactionReason.ProductSale, _ledger.Today[0].Reason);
        }

        [Test]
        public void Compra_sem_saldo_falha_sem_mexer_em_nada()
        {

            bool ok = _ledger.TryWithdraw(Y(99999), TransactionReason.FurniturePurchase);

            Assert.IsFalse(ok);
            Assert.AreEqual(Y(8000), _ledger.Balance);
            Assert.AreEqual(0, _ledger.Today.Count, "Refused attempt does not become a line.");
        }

        [Test]
        public void Compra_com_saldo_exato_passa()
        {
            Assert.IsTrue(_ledger.TryWithdraw(Y(8000), TransactionReason.FurniturePurchase));
            Assert.AreEqual(Money.Zero, _ledger.Balance);
        }

        [Test]
        public void Cobranca_obrigatoria_deixa_o_saldo_negativo()
        {

            _ledger.Charge(Y(12000), TransactionReason.Electricity, "Electricity");

            Assert.AreEqual(Y(-4000), _ledger.Balance);
            Assert.IsTrue(_ledger.Today[0].IsExpense);
        }

        [Test]
        public void Valores_nao_positivos_sao_ignorados()
        {
            _ledger.Deposit(Money.Zero, TransactionReason.ProductSale);
            _ledger.Deposit(Y(-50), TransactionReason.ProductSale);
            _ledger.Charge(Money.Zero, TransactionReason.Rent);

            Assert.IsFalse(_ledger.TryWithdraw(Money.Zero, TransactionReason.StockPurchase));
            Assert.AreEqual(Y(8000), _ledger.Balance);
            Assert.AreEqual(0, _ledger.Today.Count);
        }

        [Test]
        public void Saldo_ja_esta_atualizado_quando_o_evento_chega()
        {

            Money seenInHandler = Money.Zero;
            using (_events.Subscribe<BalanceChanged>(_ => seenInHandler = _ledger.Balance))
            {
                _ledger.Deposit(Y(100), TransactionReason.ProductSale);
            }

            Assert.AreEqual(Y(8100), seenInHandler);
        }

        [Test]
        public void Evento_de_saldo_carrega_anterior_atual_e_delta()
        {
            BalanceChanged captured = default;
            using (_events.Subscribe<BalanceChanged>(e => captured = e))
            {
                _ledger.Charge(Y(100), TransactionReason.Rent);
            }

            Assert.AreEqual(Y(8000), captured.Previous);
            Assert.AreEqual(Y(7900), captured.Current);
            Assert.AreEqual(Y(-100), captured.Delta);
        }

        [Test]
        public void Virar_o_dia_esvazia_o_livro_do_dia_mas_nao_o_saldo()
        {
            _ledger.Deposit(Y(500), TransactionReason.ProductSale);
            Assert.AreEqual(1, _ledger.Today.Count);

            _clock.AdvanceDay();

            Assert.AreEqual(0, _ledger.Today.Count);
            Assert.AreEqual(Y(8500), _ledger.Balance);
        }

        [Test]
        public void Movimentacao_carrega_o_dia_em_que_aconteceu()
        {
            _clock.AdvanceDay();               
            _ledger.Deposit(Y(100), TransactionReason.ProductSale);

            Assert.AreEqual(2, _ledger.Today[0].Day);
        }

        [Test]
        public void CanAfford_responde_pelo_saldo_atual()
        {
            Assert.IsTrue(_ledger.CanAfford(Y(8000)));
            Assert.IsFalse(_ledger.CanAfford(Y(8001)));
        }
    }
}
