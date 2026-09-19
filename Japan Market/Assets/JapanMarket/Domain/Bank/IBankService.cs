using System;
using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;

namespace JapanMarket.Domain
{
    public interface IBankService
    {
        IReadOnlyList<ActiveLoan> ActiveLoans { get; }

        /// <summary>Quanto falta pagar no total. É o número da tela do Banco.</summary>
        Money TotalDebt { get; }

        /// <summary>Soma das parcelas diárias — o peso fixo no fechamento do dia.</summary>
        Money DailyDebtService { get; }

        /// <summary>Quantos empréstimos podem estar abertos ao mesmo tempo. 0 = sem limite.</summary>
        int MaxConcurrentLoans { get; set; }

        bool TryTakeLoan(LoanDefinition loan);

        bool TryPayOffEarly(ActiveLoan loan);

        event Action<ActiveLoan> LoanTaken;
        event Action<ActiveLoan> LoanPaidOff;
    }
}

