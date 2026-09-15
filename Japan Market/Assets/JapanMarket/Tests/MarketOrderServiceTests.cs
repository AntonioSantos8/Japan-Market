using JapanMarket.Core;
using JapanMarket.Data;
using JapanMarket.Domain;
using NUnit.Framework;
using UnityEngine;

namespace JapanMarket.Tests
{
    [TestFixture]
    public class MarketOrderServiceTests
    {
        private Ledger _ledger;
        private PricingService _pricing;
        private MarketOrderService _market;
        private ItemDefinition _productA;
        private ItemDefinition _productB;

        [SetUp]
        public void SetUp()
        {
            _ledger = new Ledger(null, null, Money.FromYen(5000));
            _pricing = new PricingService();
            _market = new MarketOrderService(_ledger, _pricing);

            _productA = ScriptableObject.CreateInstance<ItemDefinition>();
            JsonUtility.FromJsonOverwrite(@"{""_baseCost"":{""Yen"":100},""_unitsPerBox"":8}", _productA);

            _productB = ScriptableObject.CreateInstance<ItemDefinition>();
            JsonUtility.FromJsonOverwrite(@"{""_baseCost"":{""Yen"":200},""_unitsPerBox"":4}", _productB);
        }

        [Test]
        public void Checkout_FailsIfCartIsEmpty()
        {
            var cart = new MarketCart();
            bool result = _market.TryCheckout(cart);

            Assert.IsFalse(result);
            Assert.AreEqual(Money.FromYen(5000), _ledger.Balance);
        }

        [Test]
        public void Checkout_FailsIfInsufficientFunds()
        {
            var cart = new MarketCart();

            cart.AddBoxes(_productA, 10);

            bool result = _market.TryCheckout(cart);

            Assert.IsFalse(result);
            Assert.AreEqual(Money.FromYen(5000), _ledger.Balance, "Balance intact in case of failure");
            Assert.AreEqual(0, _market.DeliveryQueue.PendingBoxes);
            Assert.AreEqual(10, cart.TotalBoxes, "Cart is not emptied if it fails");
        }

        [Test]
        public void Checkout_Success_DeductsBalanceAndEnqueuesDelivery()
        {
            var cart = new MarketCart();

            cart.AddBoxes(_productA, 2);
            cart.AddBoxes(_productB, 1);

            _pricing.RecordRestock(_productA, Money.FromYen(50), 10);

            bool orderEventFired = false;
            _market.OrderPlaced += (_) => orderEventFired = true;

            bool result = _market.TryCheckout(cart);

            Assert.IsTrue(result);
            Assert.AreEqual(Money.FromYen(2600), _ledger.Balance); 

            Assert.AreEqual(3, _market.DeliveryQueue.PendingBoxes);
            Assert.AreEqual(3, cart.TotalBoxes, "Cart is not cleaned internally");
            Assert.IsTrue(orderEventFired);

            var dataA = _pricing.GetPricingData(_productA);
            Assert.AreEqual(26, dataA.UnitsInAverage);
            Assert.AreEqual(Money.FromYen(50), dataA.LastCost);
            Assert.AreEqual(Money.FromYen(100), dataA.CurrentCost);

            var dataB = _pricing.GetPricingData(_productB);
            Assert.AreEqual(4, dataB.UnitsInAverage);
            Assert.AreEqual(Money.FromYen(200), dataB.CurrentCost);
        }
    }
}
