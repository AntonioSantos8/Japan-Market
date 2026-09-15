using System;
using JapanMarket.Core;

namespace JapanMarket.Domain
{

    public sealed class StoreLevelService : IStoreLevelService, IDisposable
    {
        private readonly IEventBus _events;
        private IDisposable _saleSubscription;

        public int CurrentLevel { get; private set; }
        public int CurrentXP { get; private set; }

        public event Action<int> LevelChanged;
        public event Action<int> XPChanged;

        public StoreLevelService(IEventBus events, int startingLevel = 1, int startingXP = 0)
        {
            _events = events;
            CurrentLevel = startingLevel > 0 ? startingLevel : 1;
            CurrentXP = startingXP > 0 ? startingXP : 0;

            if (_events != null)
                _saleSubscription = _events.Subscribe<SaleCompleted>(OnSaleCompleted);
        }

        public int GetXPForNextLevel()
        {

            return 100 + (50 * (CurrentLevel - 1));
        }

        private void OnSaleCompleted(SaleCompleted sale)
        {

            int xpEarned = 10;
            if (sale.ItemCount > 1) xpEarned += (sale.ItemCount - 1) * 2;

            AddXP(xpEarned);
        }

        public void AddXP(int amount)
        {
            if (amount <= 0) return;

            CurrentXP += amount;
            CheckLevelUp();
            XPChanged?.Invoke(CurrentXP);
        }

        private void CheckLevelUp()
        {
            bool leveledUp = false;

            while (CurrentXP >= GetXPForNextLevel())
            {
                CurrentXP -= GetXPForNextLevel();
                CurrentLevel++;
                leveledUp = true;
            }

            if (leveledUp)
                LevelChanged?.Invoke(CurrentLevel);
        }

        public void Restore(int level, int xp)
        {
            CurrentLevel = level > 0 ? level : 1;
            CurrentXP = xp >= 0 ? xp : 0;
        }

        public void Dispose()
        {
            _saleSubscription?.Dispose();
            _saleSubscription = null;
        }
    }
}

