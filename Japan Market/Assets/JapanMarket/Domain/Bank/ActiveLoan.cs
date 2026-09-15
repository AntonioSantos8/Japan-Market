using System;
using JapanMarket.Core;
using JapanMarket.Data;

namespace JapanMarket.Domain
{

    public sealed class ActiveLoan
    {
        public LoanDefinition Definition { get; }

        public int PaymentsMade { get; private set; }

        public ActiveLoan(LoanDefinition definition, int paymentsMade = 0)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            PaymentsMade = paymentsMade;
        }

        public Money DailyPayment => Definition.DailyPayment;

        public int PaymentsRemaining => Definition.TermDays - PaymentsMade;

        public Money BalanceToPayOff => Definition.DailyPayment * PaymentsRemaining;

        public bool IsPaidOff => PaymentsRemaining <= 0;

        internal void RecordPayment()
        {
            if (PaymentsMade < Definition.TermDays)
                PaymentsMade++;
        }
    }
}

