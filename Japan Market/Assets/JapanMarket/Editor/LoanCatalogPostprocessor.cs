using JapanMarket.Data;
using UnityEditor;

namespace JapanMarket.EditorTools
{
    /// <summary>Mantém o LoanCatalog em dia com os assets do projeto.</summary>
    public sealed class LoanCatalogPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!CatalogSync.TouchesAssets(imported, deleted, moved)) return;
            SyncNow(logWhenUnchanged: false);
        }

        [MenuItem("Japan Market/Catálogo/Reconstruir empréstimos", priority = 108)]
        public static void RebuildFromMenu() => SyncNow(logWhenUnchanged: true);

        [MenuItem("Japan Market/Catálogo/Validar empréstimos", priority = 109)]
        public static void ValidateFromMenu() => CatalogSync.Validate<LoanCatalog>();

        private static void SyncNow(bool logWhenUnchanged) =>
            CatalogSync.Sync<LoanCatalog, LoanDefinition>(logWhenUnchanged, CompareForDisplay);

        /// <summary>Do empréstimo mais barato para o mais caro: é a ordem da tela.</summary>
        private static int CompareForDisplay(LoanDefinition a, LoanDefinition b) =>
            a.Principal.Yen != b.Principal.Yen
                ? a.Principal.Yen.CompareTo(b.Principal.Yen)
                : string.CompareOrdinal(a.name, b.name);
    }
}
