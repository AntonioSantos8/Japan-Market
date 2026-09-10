using System.Collections.Generic;
using JapanMarket.Core;

namespace JapanMarket.Data
{
    /// <summary>
    /// A fonte única de verdade sobre quais produtos existem.
    ///
    /// Substitui seis listas independentes arrastadas no Inspector
    /// (ItemManager, CashRegister, GlobalPrices, ShopBuyItems, ExpansionStore e o
    /// catálogo paralelo de ícones). Cada uma delas podia divergir das outras sem
    /// erro: um produto ausente de uma vendia por ¥0, ausente de outra não
    /// aparecia no caixa.
    /// </summary>
    public interface IItemCatalog
    {
        IReadOnlyList<ItemDefinition> All { get; }

        bool TryGet(ProductId id, out ItemDefinition definition);
        ItemDefinition Get(ProductId id);
        bool Contains(ProductId id);

        IReadOnlyList<ItemDefinition> ByCategory(ProductCategory category);
        IReadOnlyList<ItemDefinition> ByStorage(StorageTrait trait);
        IReadOnlyList<ProductCategory> Categories { get; }

        /// <summary>Aloca uma lista nova — não chame por frame.</summary>
        List<ItemDefinition> UnlockedFor(IUnlockContext context);
    }
}
