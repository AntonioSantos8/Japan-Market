using JapanMarket.Core;
using JapanMarket.Domain;

namespace JapanMarket.Tests
{
    /// <summary>
    /// Capacidade de teste cujo dono pode ser atribuído pelo
    /// <see cref="FakeFurniture"/>.
    ///
    /// Em produção o dono vem do <c>GetComponentInParent</c> no Awake e é
    /// somente leitura. Nos testes não existe hierarquia de GameObject, então
    /// alguém precisa dizer de quem a capacidade é — e é melhor um contrato
    /// explícito do que o FakeFurniture conhecer cada dublê pelo nome da classe.
    /// </summary>
    public interface IOwnedTestCapability : IFurnitureCapability
    {
        new IFurniture Owner { get; set; }
    }

    /// <summary>Um móvel elétrico de mentira, para a conta de luz.</summary>
    public sealed class FakePowerConsumer : IOwnedTestCapability, IPowerConsumer
    {
        public FakePowerConsumer(long dailyYen, bool poweredOn = true)
        {
            DailyCost = Money.FromYen(dailyYen);
            IsPoweredOn = poweredOn;
        }

        public IFurniture Owner { get; set; }

        public Money DailyCost { get; set; }
        public bool IsPoweredOn { get; private set; }

        public void SetPowered(bool on) => IsPoweredOn = on;
    }
}
