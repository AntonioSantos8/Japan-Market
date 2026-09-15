using System.Collections.Generic;
using JapanMarket.Core;

namespace JapanMarket.Data
{

    public interface IItemCatalog
    {
        IReadOnlyList<ItemDefinition> All { get; }

        bool TryGet(ProductId id, out ItemDefinition definition);
        ItemDefinition Get(ProductId id);
        bool Contains(ProductId id);

        IReadOnlyList<ItemDefinition> ByCategory(ProductCategory category);
        IReadOnlyList<ItemDefinition> ByStorage(StorageTrait trait);
        IReadOnlyList<ProductCategory> Categories { get; }

        List<ItemDefinition> UnlockedFor(IUnlockContext context);
    }
}
