using System.Collections.Generic;
using System.Linq;
using JapanMarket.Data;
using UnityEditor;
using UnityEngine;

namespace JapanMarket.EditorTools
{
    /// <summary>
    /// Mantém o ItemCatalog sincronizado com os assets do projeto.
    ///
    /// É esta classe que faz "cadastrar um produto = criar um asset" ser verdade.
    /// Sem ela, voltaríamos ao problema atual: seis listas arrastadas à mão que
    /// podem divergir em silêncio.
    ///
    /// Roda a cada import, mas só grava se a lista realmente mudou — importar um
    /// PNG não suja o catálogo.
    /// </summary>
    public sealed class ItemCatalogPostprocessor : AssetPostprocessor
    {
        private const string ItemFilter    = "t:" + nameof(ItemDefinition);
        private const string CatalogFilter = "t:" + nameof(ItemCatalog);

        private static void OnPostprocessAllAssets(
            string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!TouchesProducts(imported, deleted, moved)) return;
            Sync(logResult: false);
        }

        [MenuItem("Japan Market/Catálogo/Reconstruir catálogo", priority = 100)]
        public static void RebuildFromMenu() => Sync(logResult: true);

        [MenuItem("Japan Market/Catálogo/Validar catálogo", priority = 101)]
        public static void ValidateFromMenu()
        {
            ItemCatalog catalog = FindCatalog();
            if (catalog == null) { LogNoCatalog(); return; }

            catalog.Rebuild();
            List<ItemCatalog.Problem> problems = catalog.Validate();

            if (problems.Count == 0)
            {
                Debug.Log($"[Catálogo] {catalog.All.Count} produtos, nenhum problema.", catalog);
                return;
            }

            foreach (ItemCatalog.Problem p in problems)
            {
                Object ctx = p.Item != null ? p.Item : (Object)catalog;
                string where = p.Item != null ? p.Item.name : "catálogo";
                Debug.LogWarning($"[Catálogo] {where}: {p.Message}", ctx);
            }

            Debug.LogWarning($"[Catálogo] {problems.Count} problema(s). Veja acima.", catalog);
        }

        // ── interno ──────────────────────────────────────────────────────────

        private static void Sync(bool logResult)
        {
            ItemCatalog catalog = FindCatalog();
            if (catalog == null)
            {
                if (logResult) LogNoCatalog();
                return;
            }

            List<ItemDefinition> found = AssetDatabase
                .FindAssets(ItemFilter)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ItemDefinition>)
                .Where(d => d != null)
                .OrderBy(d => d.Category != null ? d.Category.SortOrder : int.MaxValue)
                .ThenBy(d => d.name, System.StringComparer.Ordinal)
                .ToList();

            if (!catalog.EditorSetItems(found))
            {
                if (logResult) Debug.Log($"[Catálogo] Já estava sincronizado " +
                                         $"({found.Count} produtos).", catalog);
                return;
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);

            Debug.Log($"[Catálogo] Sincronizado: {found.Count} produtos.", catalog);
        }

        private static bool TouchesProducts(string[] imported, string[] deleted, string[] moved)
        {
            return Any(imported) || Any(deleted) || Any(moved);

            static bool Any(string[] paths)
            {
                if (paths == null) return false;
                foreach (string path in paths)
                    if (path.EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase))
                        return true;
                return false;
            }
        }

        private static ItemCatalog FindCatalog()
        {
            string[] guids = AssetDatabase.FindAssets(CatalogFilter);
            if (guids.Length == 0) return null;

            if (guids.Length > 1)
            {
                Debug.LogError("[Catálogo] Existe mais de um ItemCatalog no projeto. " +
                               "Deve haver exatamente um — apague os extras.");
            }

            return AssetDatabase.LoadAssetAtPath<ItemCatalog>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static void LogNoCatalog() =>
            Debug.LogWarning("[Catálogo] Nenhum ItemCatalog encontrado. " +
                             "Crie um em Assets → Create → Japan Market → Item Catalog.");
    }
}
