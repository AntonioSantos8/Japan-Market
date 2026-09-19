using JapanMarket.Core;
using JapanMarket.Data;
using JapanMarket.Domain;
using NUnit.Framework;
using UnityEngine;

namespace JapanMarket.Tests
{
    [TestFixture]
    public class BankServiceTests
    {
        private EventBus _bus;
        private Ledger _ledger;
        private ExpenseService _expenses;
        private BankService _bank;

        private FakeUnlockContext _context;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();

            _ledger = new Ledger(_bus, null, Money.FromYen(1000));
            _expenses = new ExpenseService(_ledger);
            _context = new FakeUnlockContext();

            _bank = new BankService(_ledger, _expenses, _context, _bus)
            {
                // Vários testes deste arquivo contratam um empréstimo só; os que
                // testam o teto ajustam o valor.
                MaxConcurrentLoans = 1,
            };
        }

        [Test]
        public void TakeLoan_AddsPrincipalToLedger_AndRegistersExpense()
        {
            var def = ScriptableObject.CreateInstance<LoanDefinition>();

            JsonUtility.FromJsonOverwrite(@"{""_principal"":{""_yen"":5000},""_dailyPayment"":{""_yen"":600},""_termDays"":10}", def);

            Assert.AreEqual(0, _bank.ActiveLoans.Count);
            Assert.AreEqual(Money.FromYen(1000), _ledger.Balance);

            bool success = _bank.TryTakeLoan(def);

            Assert.IsTrue(success);
            Assert.AreEqual(1, _bank.ActiveLoans.Count);

            Assert.AreEqual(Money.FromYen(6000), _ledger.Balance);

            var expenses = _expenses.Preview();
            Assert.AreEqual(1, expenses.Count);
            Assert.AreEqual(Money.FromYen(600), expenses[0].Amount);
        }

        [Test]
        public void TakeLoan_FailsIfAlreadyActive()
        {
            // Sem limite de empréstimos simultâneos, para que o que falhe aqui
            // seja o guard de MESMA FAIXA e não o teto — com MaxConcurrentLoans
            // em 1 este teste passava sem nunca chegar perto do que o nome dele
            // promete.
            _bank.MaxConcurrentLoans = 0;

            var def = ScriptableObject.CreateInstance<LoanDefinition>();
            JsonUtility.FromJsonOverwrite(@"{""_principal"":{""_yen"":5000},""_dailyPayment"":{""_yen"":600},""_termDays"":10}", def);

            _bank.TryTakeLoan(def);
            Assert.AreEqual(1, _bank.ActiveLoans.Count);

            bool success = _bank.TryTakeLoan(def);
            Assert.IsFalse(success);
            Assert.AreEqual(1, _bank.ActiveLoans.Count);
        }

        [Test]
        public void DayEnded_AdvancesPayment_AndRemovesWhenPaidOff()
        {
            var def = ScriptableObject.CreateInstance<LoanDefinition>();

            JsonUtility.FromJsonOverwrite(@"{""_principal"":{""_yen"":200},""_dailyPayment"":{""_yen"":100},""_termDays"":2}", def);

            _bank.TryTakeLoan(def);
            var loan = _bank.ActiveLoans[0];

            Assert.AreEqual(0, loan.PaymentsMade);

            _bus.Publish(new DayEnded(1));
            Assert.AreEqual(1, loan.PaymentsMade);
            Assert.AreEqual(1, _bank.ActiveLoans.Count); 

            _bus.Publish(new DayEnded(2));
            Assert.AreEqual(2, loan.PaymentsMade);
            Assert.AreEqual(0, _bank.ActiveLoans.Count, "Empréstimo quitado sai da lista.");

            Assert.AreEqual(0, _expenses.Preview().Count);
        }

        [Test]
        public void PayOffEarly_WithdrawsRemainingBalance_AndRemovesLoan()
        {
            var def = ScriptableObject.CreateInstance<LoanDefinition>();

            JsonUtility.FromJsonOverwrite(@"{""_principal"":{""_yen"":5000},""_dailyPayment"":{""_yen"":600},""_termDays"":10}", def);

            _bank.TryTakeLoan(def);
            var loan = _bank.ActiveLoans[0];

            Assert.AreEqual(Money.FromYen(6000), _ledger.Balance);

            bool paid = _bank.TryPayOffEarly(loan);

            Assert.IsTrue(paid);
            Assert.AreEqual(0, _bank.ActiveLoans.Count);
            Assert.AreEqual(Money.Zero, _ledger.Balance); 
            Assert.AreEqual(0, _expenses.Preview().Count);
        }

        [Test]
        public void PayOffEarly_FailsIfInsufficientFunds()
        {
            var def = ScriptableObject.CreateInstance<LoanDefinition>();

            JsonUtility.FromJsonOverwrite(@"{""_principal"":{""_yen"":5000},""_dailyPayment"":{""_yen"":1000},""_termDays"":10}", def);

            _bank.TryTakeLoan(def);
            var loan = _bank.ActiveLoans[0];

            bool paid = _bank.TryPayOffEarly(loan);

            Assert.IsFalse(paid, "Não dá para quitar sem saldo.");
            Assert.AreEqual(1, _bank.ActiveLoans.Count, "O empréstimo continua aberto.");
        }

        [Test]
        public void Restore_descarta_contrato_cuja_faixa_foi_apagada()
        {
            // O asset do empréstimo pode sumir do projeto entre uma versão e
            // outra. Ler o nome dele para montar a linha de despesa derrubava o
            // carregamento inteiro: o jogador perdia a partida por causa de um
            // empréstimo que nem existe mais.
            var def = ScriptableObject.CreateInstance<LoanDefinition>();
            JsonUtility.FromJsonOverwrite(
                @"{""_principal"":{""_yen"":5000},""_dailyPayment"":{""_yen"":600},""_termDays"":10}", def);

            var orfao = new ActiveLoan(def);
            Object.DestroyImmediate(def);   // o asset sumiu do projeto

            Assert.DoesNotThrow(() => _bank.Restore(new[] { orfao }));
            Assert.AreEqual(0, _bank.ActiveLoans.Count);
            Assert.AreEqual(0, _expenses.Preview().Count);
        }
    
        // ── contratos inválidos ──────────────────────────────────────────────

        [Test]
        public void Prazo_zero_nao_e_contrato()
        {
            // Sem prazo o empréstimo é depositado e quitado no primeiro
            // fechamento — dinheiro de graça depois de uma parcela.
            var def = ScriptableObject.CreateInstance<LoanDefinition>();
            JsonUtility.FromJsonOverwrite(
                @"{""_principal"":{""_yen"":5000},""_dailyPayment"":{""_yen"":600},""_termDays"":0}", def);

            Assert.IsFalse(_bank.TryTakeLoan(def));
            Assert.AreEqual(Money.FromYen(1000), _ledger.Balance);
        }

        [Test]
        public void Parcela_zero_nao_e_contrato()
        {
            // O ExpenseService filtra despesa não positiva: a parcela nunca
            // seria cobrada, mas o contador de parcelas andaria. Empréstimo
            // grátis que se auto-quita.
            var def = ScriptableObject.CreateInstance<LoanDefinition>();
            JsonUtility.FromJsonOverwrite(
                @"{""_principal"":{""_yen"":5000},""_dailyPayment"":{""_yen"":0},""_termDays"":10}", def);

            Assert.IsFalse(_bank.TryTakeLoan(def));
            Assert.AreEqual(Money.FromYen(1000), _ledger.Balance);
        }

        [Test]
        public void Nao_da_para_empilhar_todas_as_faixas_de_uma_vez()
        {
            // O guard por instância de LoanDefinition não segurava nada:
            // bastava contratar cada faixa uma vez para o banco virar fonte
            // infinita de dinheiro no primeiro dia.
            var a = ScriptableObject.CreateInstance<LoanDefinition>();
            var b = ScriptableObject.CreateInstance<LoanDefinition>();
            JsonUtility.FromJsonOverwrite(
                @"{""_principal"":{""_yen"":5000},""_dailyPayment"":{""_yen"":600},""_termDays"":10}", a);
            JsonUtility.FromJsonOverwrite(
                @"{""_principal"":{""_yen"":9000},""_dailyPayment"":{""_yen"":900},""_termDays"":10}", b);

            Assert.IsTrue(_bank.TryTakeLoan(a));
            Assert.IsFalse(_bank.TryTakeLoan(b), "Um empréstimo por vez, por padrão.");

            _bank.MaxConcurrentLoans = 2;
            Assert.IsTrue(_bank.TryTakeLoan(b));

            Assert.AreEqual(Money.FromYen(1500), _bank.DailyDebtService);
            Assert.AreEqual(Money.FromYen(15000), _bank.TotalDebt);
        }

        [Test]
        public void Faixa_travada_por_nivel_nao_e_contratavel()
        {
            var def = ScriptableObject.CreateInstance<LoanDefinition>();
            JsonUtility.FromJsonOverwrite(
                @"{""_principal"":{""_yen"":5000},""_dailyPayment"":{""_yen"":600},""_termDays"":10}", def);

            var gate = ScriptableObject.CreateInstance<StoreLevelUnlock>();
            JsonUtility.FromJsonOverwrite(@"{""_requiredLevel"":10}", gate);

            typeof(LoanDefinition)
                .GetField("_unlock", System.Reflection.BindingFlags.NonPublic
                                   | System.Reflection.BindingFlags.Instance)
                .SetValue(def, gate);

            _context.StoreLevel = 1;
            Assert.IsFalse(_bank.TryTakeLoan(def));

            _context.StoreLevel = 10;
            Assert.IsTrue(_bank.TryTakeLoan(def));
        }

        [Test]
        public void O_numero_de_parcelas_cobradas_bate_com_o_prazo()
        {
            // O teste que faltava: o outro publica DayEnded na mão e nunca passa
            // pelo ChargeDay, então prova que o contador anda e não prova nada
            // sobre dinheiro. Aqui as duas coisas andam juntas, na ordem do
            // DayCycle — cobrar, depois contar.
            var def = ScriptableObject.CreateInstance<LoanDefinition>();
            JsonUtility.FromJsonOverwrite(
                @"{""_principal"":{""_yen"":300},""_dailyPayment"":{""_yen"":100},""_termDays"":3}", def);

            _bank.TryTakeLoan(def);
            Assert.AreEqual(Money.FromYen(1300), _ledger.Balance);

            for (int day = 1; day <= 4; day++)
            {
                _expenses.ChargeDay();
                _bus.Publish(new DayEnded(day));
            }

            Assert.AreEqual(0, _bank.ActiveLoans.Count);
            Assert.AreEqual(Money.FromYen(1000), _ledger.Balance,
                "300 emprestados, 3 × 100 cobrados — e nada no quarto dia.");
        }

        [Test]
        public void Emprestimo_sem_principal_nao_e_contrato()
        {
            // O livro-razão recusa depósito não positivo: o jogador assinaria um
            // contrato que não deposita nada e passaria dez dias pagando.
            var def = ScriptableObject.CreateInstance<LoanDefinition>();
            JsonUtility.FromJsonOverwrite(
                @"{""_principal"":{""_yen"":0},""_dailyPayment"":{""_yen"":600},""_termDays"":10}", def);

            Assert.IsFalse(_bank.TryTakeLoan(def));
            Assert.AreEqual(Money.FromYen(1000), _ledger.Balance);
            Assert.AreEqual(0, _expenses.Preview().Count, "E não registra parcela nenhuma.");
        }
    }
}