using JapanMarket.Data;
using UnityEditor;

namespace JapanMarket.EditorTools
{
    /// <summary>
    /// Mantém o TrashCatalog em dia com os assets do projeto.
    ///
    /// É o que faz "cadastrar um lixo = criar um asset" ser verdade, e o que
    /// elimina o array <c>trashDatas</c> que o TrashSystem legado tinha no
    /// Inspector — uma lista paralela que podia esquecer um lixo sem erro nenhum.
    /// </summary>
    public sealed class TrashCatalogPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!CatalogSync.TouchesAssets(imported, deleted, moved)) return;
            SyncNow(logWhenUnchanged: false);
        }

        [MenuItem("Japan Market/Catálogo/Reconstruir lixo", priority = 106)]
        public static void RebuildFromMenu() => SyncNow(logWhenUnchanged: true);

        [MenuItem("Japan Market/Catálogo/Validar lixo", priority = 107)]
        public static void ValidateFromMenu() => CatalogSync.Validate<TrashCatalog>();

        private static void SyncNow(bool logWhenUnchanged) =>
            CatalogSync.Sync<TrashCatalog, TrashDefinition>(logWhenUnchanged, CompareForDisplay);

        /// <summary>Agrupado por categoria, e alfabético dentro dela.</summary>
        private static int CompareForDisplay(TrashDefinition a, TrashDefinition b)
        {
            int orderA = a.Category != null ? a.Category.SortOrder : int.MaxValue;
            int orderB = b.Category != null ? b.Category.SortOrder : int.MaxValue;

            return orderA != orderB
                ? orderA.CompareTo(orderB)
                : string.CompareOrdinal(a.name, b.name);
        }
    }
}
