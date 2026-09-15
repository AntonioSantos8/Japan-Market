using System;
using JapanMarket.Core;

namespace JapanMarket.Domain
{

    public sealed class GameClock : IGameClock
    {
        private readonly IEventBus _events;

        private const float NotifyStepHours = 1f / 60f;

        private float _lastNotifiedTime;

        private int _lastClosedDay;

        public GameClock(IEventBus events, GameClockSettings settings = default)
        {
            _events = events;
            Settings = settings.OrDefault();

            Day = 1;
            TimeOfDay = Settings.DayStartHour;
            _lastNotifiedTime = TimeOfDay;
        }

        public GameClockSettings Settings { get; }

        public int Day { get; private set; }
        public float TimeOfDay { get; private set; }
        public bool StoreIsOpen { get; private set; }
        public bool IsRunning { get; private set; } = true;

        public float ClosingHour => Settings.ClosingHour;
        public float EndOfDayHour => Settings.EndOfDayHour;

        public event Action<int> EndOfDayReached;
        public event Action<IGameClock> TimeChanged;

        public void Tick(float deltaSeconds)
        {
            if (!IsRunning || deltaSeconds <= 0f) return;

            TimeOfDay += deltaSeconds * Settings.GameHoursPerRealSecond;

            if (StoreIsOpen && TimeOfDay >= Settings.ClosingHour) CloseStore();

            if (_lastClosedDay != Day && TimeOfDay >= Settings.EndOfDayHour) FireEndOfDay();

            if (Math.Abs(TimeOfDay - _lastNotifiedTime) < NotifyStepHours) return;

            _lastNotifiedTime = TimeOfDay;
            TimeChanged?.Invoke(this);
        }

        public void SetRunning(bool running) => IsRunning = running;

        public bool TryOpenStore()
        {
            if (StoreIsOpen) return true;
            if (TimeOfDay >= Settings.ClosingHour) return false;

            StoreIsOpen = true;
            _events?.Publish(new StoreOpenStateChanged(true));
            return true;
        }

        public void CloseStore()
        {
            if (!StoreIsOpen) return;

            StoreIsOpen = false;
            _events?.Publish(new StoreOpenStateChanged(false));
        }

        public void RequestEndOfDay()
        {
            if (_lastClosedDay == Day) return;
            FireEndOfDay();
        }

        private void FireEndOfDay()
        {

            _lastClosedDay = Day;

            CloseStore();
            EndOfDayReached?.Invoke(Day);
        }

        public void AdvanceDay()
        {
            Day++;
            TimeOfDay = Settings.DayStartHour;
            _lastNotifiedTime = TimeOfDay;

            _events?.Publish(new DayStarted(Day));
            TimeChanged?.Invoke(this);
        }

        public void Restore(int day, float timeOfDay, bool storeOpen)
        {
            Day = day < 1 ? 1 : day;
            TimeOfDay = Math.Max(0f, timeOfDay);
            _lastNotifiedTime = TimeOfDay;

            _lastClosedDay = TimeOfDay >= Settings.EndOfDayHour ? Day : Day - 1;

            StoreIsOpen = storeOpen && TimeOfDay < Settings.ClosingHour;
        }

        public override string ToString()
        {
            int hour = (int)TimeOfDay;
            int minute = (int)((TimeOfDay - hour) * 60f);
            return $"Day {Day}, {hour:00}:{minute:00}{(StoreIsOpen ? ", open" : "")}";
        }
    }

    [Serializable]
    public struct GameClockSettings
    {
        public float DayStartHour;
        public float ClosingHour;
        public float EndOfDayHour;

        public float GameHoursPerRealSecond;

        public static GameClockSettings Default => new()
        {
            DayStartHour = 6f,
            ClosingHour = 22f,
            EndOfDayHour = 24f,
            GameHoursPerRealSecond = 0.2f,
        };

        public GameClockSettings OrDefault()
        {
            GameClockSettings fallback = Default;

            if (GameHoursPerRealSecond <= 0f) GameHoursPerRealSecond = fallback.GameHoursPerRealSecond;
            if (EndOfDayHour <= 0f) EndOfDayHour = fallback.EndOfDayHour;
            if (ClosingHour <= 0f || ClosingHour > EndOfDayHour) ClosingHour = Math.Min(fallback.ClosingHour, EndOfDayHour);

            if (DayStartHour <= 0f || DayStartHour >= ClosingHour)
                DayStartHour = Math.Min(fallback.DayStartHour, ClosingHour);

            return this;
        }
    }
}
