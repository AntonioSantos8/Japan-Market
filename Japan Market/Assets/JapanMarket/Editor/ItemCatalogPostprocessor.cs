using JapanMarket.Data;
using UnityEditor;

namespace JapanMarket.EditorTools
{
    /// <summary>
    /// Mantém o ItemCatalog em dia com os assets do projeto.
    ///
    /// É esta classe que faz "cadastrar um produto = criar um asset" ser verdade.
    /// Sem ela, voltaríamos ao problema atual: seis listas arrastadas à mão que
    /// podem divergir em silêncio.
    /// </summary>
    public sealed class ItemCatalogPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!CatalogSync.TouchesAssets(imported, deleted, moved)) return;
            SyncNow(logWhenUnchanged: false);
        }

        [MenuItem("Japan Market/Catálogo/Reconstruir produtos", priority = 100)]
        public static void RebuildFromMenu() => SyncNow(logWhenUnchanged: true);

        [MenuItem("Japan Market/Catálogo/Validar produtos", priority = 101)]
        public static void ValidateFromMenu() => CatalogSync.Validate<ItemCatalog>();

        private static void SyncNow(bool logWhenUnchanged) =>
            CatalogSync.Sync<ItemCatalog, ItemDefinition>(logWhenUnchanged, CompareForDisplay);

        /// <summary>Ordem de catálogo: categoria primeiro, nome depois.</summary>
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
