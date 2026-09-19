using JapanMarket.Data;
using UnityEditor;

namespace JapanMarket.EditorTools
{

    public sealed class ItemCatalogPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!CatalogSync.TouchesAssets(imported, deleted, moved)) return;
            SyncNow(logWhenUnchanged: false);
        }

        [MenuItem("Japan Market/Catalog/Rebuild products", priority = 100)]
        public static void RebuildFromMenu() => SyncNow(logWhenUnchanged: true);

        [MenuItem("Japan Market/Catalog/Validate products", priority = 101)]
        public static void ValidateFromMenu() => CatalogSync.Validate<ItemCatalog>();

        private static void SyncNow(bool logWhenUnchanged) =>
            CatalogSync.Sync<ItemCatalog, ItemDefinition>(logWhenUnchanged, CompareForDisplay);

        private static int CompareForDisplay(ItemDefinition a, ItemDefinition b)
        {
            int orderA = a.Category != null ? a.Category.SortOrder : int.MaxValue;
            int orderB = b.Category != null ? b.Category.SortOrder : int.MaxValue;

            return orderA != orderB
                ? orderA.CompareTo(orderB)
                : string.CompareOrdinal(a.name, b.name);
        }
    }
}
