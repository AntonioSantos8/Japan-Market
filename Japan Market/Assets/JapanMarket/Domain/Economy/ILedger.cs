using System;
using System.Collections.Generic;
using JapanMarket.Core;

namespace JapanMarket.Domain
{

    public interface ILedger
    {
        Money Balance { get; }

        IReadOnlyList<Transaction> Today { get; }

        void Deposit(Money amount, TransactionReason reason, string note = null);

        bool TryWithdraw(Money amount, TransactionReason reason, string note = null);

        void Charge(Money amount, TransactionReason reason, string note = null);

        bool CanAfford(Money amount);

        event Action<Transaction> Recorded;
    }
}
