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

        private readonly IReadOnlyList<StoreSectionOffer> _offers;

        private readonly ILedger _ledger;
        private readonly IUnlockContext _unlocks;
        private readonly IProgressFlags _flags;

        public StoreExpansionService(ILedger ledger, IUnlockContext unlocks,
                                     IProgressFlags flags,
                                     IReadOnlyList<StoreSectionOffer> offers)
        {
            _ledger = ledger;
            _unlocks = unlocks;
            _flags = flags;
            _offers = offers ?? Array.Empty<StoreSectionOffer>();
        }

        public IReadOnlyList<StoreSectionOffer> Offers => _offers;

        public int OwnedCount
        {
            get
            {
                int count = 0;

                for (int i = 0; i < _offers.Count; i++)
                    if (IsOwned(_offers[i].Section)) count++;

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

            StoreSectionOffer offer = _offers[offerIndex];
            if (_unlocks.StoreLevel < offer.RequiredStoreLevel)
                return ExpansionPurchaseResult.StoreLevelRequired;

            if (!_ledger.TryWithdraw(offer.Price, TransactionReason.ExpansionPurchase,
                                     $"Expansão da loja — Seção {section}"))
                return ExpansionPurchaseResult.NotEnoughMoney;

            _flags.RaiseFlag(FlagFor(section));
            SectionPurchased?.Invoke(section);
            return ExpansionPurchaseResult.Ok;
        }

        private int FindOffer(int section)
        {
            for (int i = 0; i < _offers.Count; i++)
                if (_offers[i].Section == section) return i;

            return -1;
        }

        private static string FlagFor(int section) => FlagPrefix + section;
    }
}
