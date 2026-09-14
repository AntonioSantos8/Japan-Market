using System.Collections.Generic;
using JapanMarket.Core;

namespace JapanMarket.Data
{
    /// <summary>Fonte única de verdade sobre quais modelos de móvel existem.</summary>
    public interface IFurnitureCatalog
    {
        IReadOnlyList<FurnitureDefinition> All { get; }
        IReadOnlyList<FurnitureCategory> Categories { get; }

        bool TryGet(FurnitureId id, out FurnitureDefinition definition);
        FurnitureDefinition Get(FurnitureId id);
        bool Contains(FurnitureId id);

        IReadOnlyList<FurnitureDefinition> ByCategory(FurnitureCategory category);
        List<FurnitureDefinition> UnlockedFor(IUnlockContext context);
    }
}
