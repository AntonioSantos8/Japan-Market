using System;

namespace JapanMarket.Core
{
    /// <summary>
    /// Estado mínimo do tutorial exposto às interfaces. Core não conhece o
    /// TutorialManager nem as etapas; só sabe se deve esconder as metas da loja.
    /// </summary>
    public interface ITutorialStatus
    {
        bool IsFinished { get; }
        bool IsFirstDayProtected { get; }
        int FirstDayCustomersServed { get; }
        int FirstDayCustomerTarget { get; }
        event Action Completed;
    }
}
