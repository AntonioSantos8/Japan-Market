using System;

namespace JapanMarket.Domain
{

    public interface IStoreLevelService
    {
        int CurrentLevel { get; }
        int CurrentXP { get; }

        int GetXPForNextLevel();

        event Action<int> LevelChanged;
        event Action<int> XPChanged;
    }
}

