using System.Collections.Generic;
using JapanMarket.Data;
using JapanMarket.Domain;

namespace JapanMarket.Gameplay
{

    public sealed class StoreProgress : IUnlockContext
    {
        private readonly IStoreLevelService _storeLevelService;
        private readonly HashSet<string> _flags = new();

        public StoreProgress(IStoreLevelService storeLevelService = null)
        {
            _storeLevelService = storeLevelService;
        }

        public int StoreLevel => _storeLevelService?.CurrentLevel ?? 1;
        public int CurrentDay { get; private set; } = 1;

        public bool HasFlag(string flag) => !string.IsNullOrEmpty(flag) && _flags.Contains(flag);

        public void SetDay(int day) => CurrentDay = day < 1 ? 1 : day;

        public void RaiseFlag(string flag)
        {
            if (!string.IsNullOrEmpty(flag)) _flags.Add(flag);
        }

        public IReadOnlyCollection<string> Flags => _flags;
    }
}
