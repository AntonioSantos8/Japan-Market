using System;

namespace JapanMarket.Domain
{

    public interface IGameClock
    {

        int Day { get; }

        float TimeOfDay { get; }

        bool StoreIsOpen { get; }

        bool IsRunning { get; }

        float ClosingHour { get; }

        float EndOfDayHour { get; }

        void Tick(float deltaSeconds);

        bool TryOpenStore();

        void CloseStore();

        void SetRunning(bool running);

        void RequestEndOfDay();

        void AdvanceDay();

        event Action<int> EndOfDayReached;

        event Action<IGameClock> TimeChanged;
    }
}
