using System;
using JapanMarket.Core;

namespace JapanMarket.Domain
{
    /// <summary>Reusable equipped bag and its separate scraps balance.</summary>
    public sealed class PlayerTrashBag
    {
        private readonly IToolBelt _tools;
        private readonly IEventBus _events;
        private readonly int _capacity;

        public PlayerTrashBag(IToolBelt tools, IEventBus events = null)
        {
            _tools = tools;
            _events = events;
            _capacity = 10;
            if (tools == null) return;
            foreach (ToolSlot slot in tools.Slots)
                if (slot.Tool != null && slot.Tool.IsTrashBag)
                { _capacity = slot.Tool.TrashCapacity; break; }
        }

        public int Capacity => _capacity;
        public int Count { get; private set; }
        public long Scraps { get; private set; }
        public bool IsEquipped => _tools?.Selected?.IsUsable == true
            && _tools.Selected.Tool.IsTrashBag;
        public bool IsFull => Count >= Capacity;
        public bool CanCollect => IsEquipped && !IsFull;
        public bool CanEmpty => IsEquipped && Count > 0 && Scraps <= long.MaxValue - Count;
        public event Action Changed;

        public bool TryCollect(string categoryKey = "")
        {
            if (!CanCollect) return false;
            Count++;
            Changed?.Invoke();
            // Keep recycling objectives compatible with the new collection flow.
            _events?.Publish(new TrashDiscarded(categoryKey ?? "", Count, Capacity));
            return true;
        }

        public bool TryEmpty(out int earnedScraps)
        {
            earnedScraps = 0;
            if (!CanEmpty) return false;
            earnedScraps = Count;
            Scraps += Count;
            Count = 0;
            Changed?.Invoke();
            return true;
        }

        public void Restore(int count, long scraps)
        {
            Count = Math.Max(0, Math.Min(Capacity, count));
            Scraps = Math.Max(0L, scraps);
            Changed?.Invoke();
        }
    }
}
