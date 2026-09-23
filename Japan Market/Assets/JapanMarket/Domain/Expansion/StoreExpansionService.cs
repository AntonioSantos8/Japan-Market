using System;
using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Regras da expansão da loja: opções independentes, requisito de nível e
    /// débito atômico. Cada expansão comprada levanta uma flag que o save já
    /// persiste; a troca das paredes pertence ao componente de cena.
    /// </summary>
    public sealed class StoreExpansionService : IStoreExpansionService
    {
        private const string FlagPrefix = "store.section.";

        private static readonly StoreSectionOffer[] SectionOffers =
        {
            new(2, Money.FromYen(350), 4),
            new(3, Money.FromYen(600), 6),
            new(4, Money.FromYen(1100), 9),
            new(5, Money.FromYen(1600), 14),
            new(6, Money.FromYen(2500), 18),
            new(7, Money.FromYen(3400), 23),
            new(8, Money.FromYen(4600), 26),
            new(9, Money.FromYen(5850), 31),
            new(10, Money.FromYen(7500), 37),
            new(11, Money.FromYen(9500), 44),
            new(12, Money.FromYen(12000), 52),
            new(13, Money.FromYen(15000), 61),
        };

        private readonly ILedger _ledger;
        private readonly IUnlockContext _unlocks;
        private readonly IProgressFlags _flags;

        public StoreExpansionService(ILedger ledger, IUnlockContext unlocks,
                                     IProgressFlags flags)
        {
            _ledger = ledger;
            _unlocks = unlocks;
            _flags = flags;
        }

        public IReadOnlyList<StoreSectionOffer> Offers => SectionOffers;

        public int OwnedCount
        {
            get
            {
                int count = 0;

                for (int i = 0; i < SectionOffers.Length; i++)
                    if (IsOwned(SectionOffers[i].Section)) count++;

                return count;
            }
        }

        public event Action<int> SectionPurchased;

        public bool IsOwned(int section) =>
            section <= 1 || (_flags != null && _flags.HasFlag(FlagFor(section)));

        public ExpansionPurchaseResult TryPurchase(int section)
        {
            int offerIndex = FindOffer(section);
            if (offerIndex < 0) return ExpansionPurchaseResult.InvalidSection;
            if (_ledger == null || _flags == null || _unlocks == null)
                return ExpansionPurchaseResult.Unavailable;
            if (IsOwned(section)) return ExpansionPurchaseResult.AlreadyOwned;

            StoreSectionOffer offer = SectionOffers[offerIndex];
            if (_unlocks.StoreLevel < offer.RequiredStoreLevel)
                return ExpansionPurchaseResult.StoreLevelRequired;

            if (!_ledger.TryWithdraw(offer.Price, TransactionReason.ExpansionPurchase,
                                     $"Expansão da loja — Seção {section}"))
                return ExpansionPurchaseResult.NotEnoughMoney;

            _flags.RaiseFlag(FlagFor(section));
            SectionPurchased?.Invoke(section);
            return ExpansionPurchaseResult.Ok;
        }

        private static int FindOffer(int section)
        {
            for (int i = 0; i < SectionOffers.Length; i++)
                if (SectionOffers[i].Section == section) return i;

            return -1;
        }

        private static string FlagFor(int section) => FlagPrefix + section;
    }
}
