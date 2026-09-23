using System;
using System.Collections.Generic;
using JapanMarket.Core;

namespace JapanMarket.Domain
{
    public enum ExpansionPurchaseResult
    {
        Ok = 0,
        InvalidSection = 1,
        AlreadyOwned = 2,
        StoreLevelRequired = 3,
        NotEnoughMoney = 4,
        Unavailable = 5,
    }

    /// <summary>Uma seção comprável da loja.</summary>
    public readonly struct StoreSectionOffer
    {
        public StoreSectionOffer(int section, Money price, int requiredStoreLevel)
        {
            Section = section;
            Price = price;
            RequiredStoreLevel = requiredStoreLevel;
        }

        public int Section { get; }
        public Money Price { get; }
        public int RequiredStoreLevel { get; }
    }

    /// <summary>
    /// Compra independente das áreas da loja. Cada opção tem preço e requisito
    /// próprios; comprar uma nunca exige ter comprado outra. O estado é guardado
    /// nas flags de progresso, portanto participa do save sem uma segunda cópia.
    /// </summary>
    public interface IStoreExpansionService
    {
        IReadOnlyList<StoreSectionOffer> Offers { get; }
        int OwnedCount { get; }

        bool IsOwned(int section);
        ExpansionPurchaseResult TryPurchase(int section);

        event Action<int> SectionPurchased;
    }
}
