using JapanMarket.Core;
using JapanMarket.Domain;
using NUnit.Framework;

namespace JapanMarket.Tests
{
    [TestFixture]
    public class StoreLevelServiceTests
    {
        private EventBus _bus;
        private StoreLevelService _service;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _service = new StoreLevelService(_bus);
        }

        [Test]
        public void StartsAtLevelOne_WithZeroXP()
        {
            Assert.AreEqual(1, _service.CurrentLevel);
            Assert.AreEqual(0, _service.CurrentXP);
        }

        [Test]
        public void AddsXp_OnSaleCompleted()
        {
            _bus.Publish(new SaleCompleted(1, default, Money.Zero, Money.Zero, 1, PaymentMethod.Cash));

            Assert.AreEqual(10, _service.CurrentXP, "Sale of 1 item should give 10 XP");
        }

        [Test]
        public void AddsBonusXp_ForMultipleItems()
        {
            _bus.Publish(new SaleCompleted(1, default, Money.Zero, Money.Zero, 3, PaymentMethod.Cash));

            Assert.AreEqual(14, _service.CurrentXP, "Sale of 3 items: 10 + 2*2 = 14 XP");
        }

        [Test]
        public void LevelsUp_WhenXpThresholdReached()
        {
            int levelEvents = 0;
            int lastXpEvent = -1;

            _service.LevelChanged += (level) => levelEvents++;
            _service.XPChanged += (xp) => lastXpEvent = xp;

            _service.AddXP(100);

            Assert.AreEqual(2, _service.CurrentLevel);
            Assert.AreEqual(0, _service.CurrentXP, "XP should be deducted upon leveling up");
            Assert.AreEqual(1, levelEvents);
            Assert.AreEqual(0, lastXpEvent, "The XPChanged event should reflect the value after deducting the level up cost");
        }

        [Test]
        public void LevelsUpMultipleTimes_IfXpIsHighEnough()
        {

            _service.AddXP(300);

            Assert.AreEqual(3, _service.CurrentLevel);
            Assert.AreEqual(50, _service.CurrentXP);
        }

        [Test]
        public void Restore_SetsValuesWithoutEvents()
        {
            bool eventFired = false;
            _service.LevelChanged += (_) => eventFired = true;
            _service.XPChanged += (_) => eventFired = true;

            _service.Restore(5, 50);

            Assert.AreEqual(5, _service.CurrentLevel);
            Assert.AreEqual(50, _service.CurrentXP);
            Assert.IsFalse(eventFired, "Restore should not emit events");
        }
    }
}

