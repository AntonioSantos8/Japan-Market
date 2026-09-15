using System;
using System.Collections.Generic;
using JapanMarket.Data;

namespace JapanMarket.Domain
{
    public interface IBankService
    {
        IReadOnlyList<ActiveLoan> ActiveLoans { get; }

        bool TryTakeLoan(LoanDefinition loan);

        bool TryPayOffEarly(ActiveLoan loan);

        event Action<ActiveLoan> LoanTaken;
        event Action<ActiveLoan> LoanPaidOff;
    }
}

