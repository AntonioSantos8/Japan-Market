using JapanMarket.Data;
using UnityEditor;

namespace JapanMarket.EditorTools
{

    public sealed class FurnitureCatalogPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!CatalogSync.TouchesAssets(imported, deleted, moved)) return;
            SyncNow(logWhenUnchanged: false);
        }

        [MenuItem("Japan Market/Catalog/Rebuild furniture", priority = 102)]
        public static void RebuildFromMenu() => SyncNow(logWhenUnchanged: true);

        [MenuItem("Japan Market/Catalog/Validate furniture", priority = 103)]
        public static void ValidateFromMenu() => CatalogSync.Validate<FurnitureCatalog>();

        private static void SyncNow(bool logWhenUnchanged) =>
            CatalogSync.Sync<FurnitureCatalog, FurnitureDefinition>(
                logWhenUnchanged, CompareForDisplay);

        private static int CompareForDisplay(FurnitureDefinition a, FurnitureDefinition b)
        {
            int orderA = a.Category != null ? a.Category.SortOrder : int.MaxValue;
            int orderB = b.Category != null ? b.Category.SortOrder : int.MaxValue;

            return orderA != orderB
                ? orderA.CompareTo(orderB)
                : string.CompareOrdinal(a.name, b.name);
        }
    }
}
