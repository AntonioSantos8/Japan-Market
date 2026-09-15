using System.Collections.Generic;
using JapanMarket.Core;

namespace JapanMarket.Domain
{
    /// <summary>
    /// A conta de luz.
    ///
    /// Esta classe é a recompensa do modelo de capacidades. O aviso da
    /// referência — "quanto mais aparelhos elétricos, mais caras as despesas" —
    /// não é uma regra escrita em lugar nenhum: é literalmente a soma dos
    /// móveis que expõem <see cref="IPowerConsumer"/> e estão ligados.
    ///
    /// Adicionar um novo móvel elétrico ao jogo não passa por aqui. Ele entra na
    /// conta porque tem a capacidade, não porque alguém lembrou de somá-lo.
    /// </summary>
    public sealed class PowerExpense : IExpenseSource
    {
        private readonly IFurnitureRegistry _furniture;

        public PowerExpense(IFurnitureRegistry furniture, string label = "Eletricidade")
        {
            _furniture = furniture;
            Label = label;
        }

        public string Label { get; }
        public TransactionReason Reason => TransactionReason.Electricity;

        public Money GetDailyCost()
        {
            if (_furniture == null) return Money.Zero;

            IReadOnlyList<IPowerConsumer> consumers =
                _furniture.WithCapability<IPowerConsumer>();

            Money total = Money.Zero;

            for (int i = 0; i < consumers.Count; i++)
            {
                IPowerConsumer consumer = consumers[i];

                if (consumer?.Owner == null || !consumer.Owner.IsAlive) continue;
                if (!consumer.IsPoweredOn) continue;

                total += consumer.DailyCost;
            }

            return total;
        }

        /// <summary>Quantos aparelhos estão pesando na conta. Para a tela.</summary>
        public int PoweredDeviceCount
        {
            get
            {
                if (_furniture == null) return 0;

                IReadOnlyList<IPowerConsumer> consumers =
                    _furniture.WithCapability<IPowerConsumer>();

                int count = 0;
                for (int i = 0; i < consumers.Count; i++)
                {
                    IPowerConsumer consumer = consumers[i];
                    if (consumer?.Owner == null || !consumer.Owner.IsAlive) continue;
                    if (consumer.IsPoweredOn) count++;
                }

                return count;
            }
        }
    }
}
