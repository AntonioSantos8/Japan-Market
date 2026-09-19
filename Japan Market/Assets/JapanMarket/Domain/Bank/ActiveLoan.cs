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
            // `== null`, e não `??`. O operador de coalescência é do C# e passa
            // por cima da sobrecarga de `==` da Unity: um asset destruído (ou
            // apagado do projeto entre duas versões do jogo) não é null para o
            // `??`, e o contrato era aceito com uma definição morta dentro —
            // para estourar bem depois, na primeira leitura da parcela, longe
            // de onde dava para entender o que aconteceu.
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            Definition = definition;
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

