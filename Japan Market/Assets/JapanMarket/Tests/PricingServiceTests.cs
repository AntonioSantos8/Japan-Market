using JapanMarket.Core;
using JapanMarket.Data;
using JapanMarket.Domain;
using NUnit.Framework;
using UnityEngine;

namespace JapanMarket.Tests
{
    [TestFixture]
    public class PricingServiceTests
    {
        private PricingService _pricing;

        [SetUp]
        public void SetUp()
        {
            _pricing = new PricingService();
        }

        [Test]
        public void GetPricingData_ReturnsInitial_WhenNoDataExists()
        {
            var product = ScriptableObject.CreateInstance<ItemDefinition>();
            JsonUtility.FromJsonOverwrite(@"{""_baseCost"":{""Yen"":100},""_marketPrice"":{""Yen"":150}}", product);

            var data = _pricing.GetPricingData(product);

            Assert.AreEqual(Money.FromYen(150), data.SellPrice);
            Assert.AreEqual(Money.FromYen(100), data.CurrentCost);
            Assert.AreEqual(Money.Zero, data.LastCost);
            Assert.AreEqual(Money.Zero, data.AverageCost);
            Assert.AreEqual(0, data.UnitsInAverage);
            Assert.AreEqual(Money.FromYen(50), data.Profit); 
        }

        [Test]
        public void SetSellPrice_UpdatesPriceAndDay()
        {
            var product = ScriptableObject.CreateInstance<ItemDefinition>();
            JsonUtility.FromJsonOverwrite(@"{""_baseCost"":{""Yen"":100},""_marketPrice"":{""Yen"":150}}", product);

            _pricing.SetSellPrice(product, Money.FromYen(200), 5);

            var data = _pricing.GetPricingData(product);
            Assert.AreEqual(Money.FromYen(200), data.SellPrice);
            Assert.AreEqual(5, data.LastChangeDay);
            Assert.AreEqual(Money.FromYen(100), data.Profit); 
        }

        [Test]
        public void RecordRestock_UpdatesCurrentAndAverageCost()
        {
            var product = ScriptableObject.CreateInstance<ItemDefinition>();
            JsonUtility.FromJsonOverwrite(@"{""_baseCost"":{""Yen"":100},""_marketPrice"":{""Yen"":150}}", product);

            _pricing.RecordRestock(product, Money.FromYen(100), 1);

            var data1 = _pricing.GetPricingData(product);
            Assert.AreEqual(Money.FromYen(100), data1.CurrentCost);
            Assert.AreEqual(Money.FromYen(100), data1.AverageCost);
            Assert.AreEqual(1, data1.UnitsInAverage);

            _pricing.RecordRestock(product, Money.FromYen(150), 1);

            var data2 = _pricing.GetPricingData(product);
            Assert.AreEqual(Money.FromYen(100), data2.LastCost); 
            Assert.AreEqual(Money.FromYen(150), data2.CurrentCost);

            Assert.AreEqual(Money.FromYen(125), data2.AverageCost);
            Assert.AreEqual(2, data2.UnitsInAverage);
        }
    }
}

