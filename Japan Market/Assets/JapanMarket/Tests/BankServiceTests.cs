using System.Linq;
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

        private class FakeUnlockContext : IUnlockContext
        {
            public int StoreLevel { get; set; } = 1;
            public int CurrentDay { get; set; } = 1;
            public bool HasFlag(string flag) => false;
        }

        private FakeUnlockContext _context;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();

            _ledger = new Ledger(_bus, null, Money.FromYen(1000));
            _expenses = new ExpenseService(_ledger);
            _context = new FakeUnlockContext();

            _bank = new BankService(_ledger, _expenses, _context, _bus);
        }

        [Test]
        public void TakeLoan_AddsPrincipalToLedger_AndRegistersExpense()
        {
            var def = ScriptableObject.CreateInstance<LoanDefinition>();

            JsonUtility.FromJsonOverwrite(@"{""_principal"":{""Yen"":5000},""_dailyPayment"":{""Yen"":600},""_termDays"":10}", def);

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
            var def = ScriptableObject.CreateInstance<LoanDefinition>();
            JsonUtility.FromJsonOverwrite(@"{""_principal"":{""Yen"":5000},""_dailyPayment"":{""Yen"":600},""_termDays"":10}", def);

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

            JsonUtility.FromJsonOverwrite(@"{""_principal"":{""Yen"":200},""_dailyPayment"":{""Yen"":100},""_termDays"":2}", def);

            _bank.TryTakeLoan(def);
            var loan = _bank.ActiveLoans[0];

            Assert.AreEqual(0, loan.PaymentsMade);

            _bus.Publish(new DayEnded(1));
            Assert.AreEqual(1, loan.PaymentsMade);
            Assert.AreEqual(1, _bank.ActiveLoans.Count); 

            _bus.Publish(new DayEnded(2));
            Assert.AreEqual(2, loan.PaymentsMade);
            Assert.AreEqual(0, _bank.ActiveLoans.Count, "Paid off loan should be removed");

            Assert.AreEqual(0, _expenses.Preview().Count);
        }

        [Test]
        public void PayOffEarly_WithdrawsRemainingBalance_AndRemovesLoan()
        {
            var def = ScriptableObject.CreateInstance<LoanDefinition>();

            JsonUtility.FromJsonOverwrite(@"{""_principal"":{""Yen"":5000},""_dailyPayment"":{""Yen"":600},""_termDays"":10}", def);

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

            JsonUtility.FromJsonOverwrite(@"{""_principal"":{""Yen"":5000},""_dailyPayment"":{""Yen"":1000},""_termDays"":10}", def);

            _bank.TryTakeLoan(def);
            var loan = _bank.ActiveLoans[0];

            bool paid = _bank.TryPayOffEarly(loan);

            Assert.IsFalse(paid, "Should not allow paying off without balance");
            Assert.AreEqual(1, _bank.ActiveLoans.Count, "Loan remains active");
        }
    }
}

