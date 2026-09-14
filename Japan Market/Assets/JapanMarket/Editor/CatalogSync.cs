using System;
using System.Collections.Generic;
using System.Linq;
using JapanMarket.Data;
using UnityEditor;
using UnityEngine;

namespace JapanMarket.EditorTools
{
    /// <summary>
    /// A mecânica compartilhada de manter um catálogo sincronizado com os assets.
    ///
    /// Existe para que produtos, móveis e — na Fase 7 — upgrades e expansões não
    /// tenham três cópias quase idênticas de noventa linhas de código de editor.
    /// Cada postprocessor concreto vira meia dúzia de linhas sobre isto.
    /// </summary>
    internal static class CatalogSync
    {
        /// <summary>
        /// Localiza o único catálogo daquele tipo. Mais de um é erro de projeto,
        /// e é melhor gritar do que sincronizar um e deixar o outro velho.
        /// </summary>
        public static TCatalog FindSingle<TCatalog>() where TCatalog : ScriptableObject
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(TCatalog).Name}");
            if (guids.Length == 0) return null;

            if (guids.Length > 1)
            {
                Debug.LogError($"[Catálogo] Existe mais de um {typeof(TCatalog).Name} no " +
                               "projeto. Deve haver exatamente um — apague os extras.");
            }

            return AssetDatabase.LoadAssetAtPath<TCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        public static List<TDefinition> LoadAllDefinitions<TDefinition>(
            Comparison<TDefinition> sort = null) where TDefinition : ScriptableObject
        {
            List<TDefinition> found = AssetDatabase
                .FindAssets($"t:{typeof(TDefinition).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<TDefinition>)
                .Where(d => d != null)
                .ToList();

            if (sort != null) found.Sort(sort);
            else found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            return found;
        }

        public static void Sync<TCatalog, TDefinition>(
            bool logWhenUnchanged, Comparison<TDefinition> sort = null)
            where TCatalog : ScriptableObject, IEditableCatalog<TDefinition>
            where TDefinition : ScriptableObject
        {
            TCatalog catalog = FindSingle<TCatalog>();
            if (catalog == null)
            {
                if (logWhenUnchanged)
                    Debug.LogWarning($"[Catálogo] Nenhum {typeof(TCatalog).Name} encontrado. " +
                                     "Crie um em Assets → Create → Japan Market.");
                return;
            }

            List<TDefinition> definitions = LoadAllDefinitions(sort);

            if (!catalog.EditorSetItems(definitions))
            {
                if (logWhenUnchanged)
                    Debug.Log($"[Catálogo] {catalog.CatalogName} já estava sincronizado " +
                              $"({definitions.Count} entradas).", catalog);
                return;
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);

            Debug.Log($"[Catálogo] {catalog.CatalogName} sincronizado: " +
                      $"{definitions.Count} entradas.", catalog);
        }

        public static void Validate<TCatalog>() where TCatalog : ScriptableObject, IValidatableCatalog
        {
            TCatalog catalog = FindSingle<TCatalog>();
            if (catalog == null)
            {
                Debug.LogWarning($"[Catálogo] Nenhum {typeof(TCatalog).Name} encontrado.");
                return;
            }

            List<CatalogProblem> problems = catalog.Validate();

            if (problems.Count == 0)
            {
                Debug.Log($"[Catálogo] {catalog.CatalogName}: {catalog.EntryCount} entradas, " +
                          "nenhum problema.", catalog);
                return;
            }

            foreach (CatalogProblem problem in problems)
            {
                // Qualificado: este arquivo importa System E UnityEngine, então
                // 'Object' sozinho seria ambíguo.
                UnityEngine.Object context = problem.Asset != null ? problem.Asset : catalog;
                Debug.LogWarning($"[Catálogo] {catalog.CatalogName} · " +
                                 $"{problem.AssetName}: {problem.Message}", context);
            }

            Debug.LogWarning($"[Catálogo] {catalog.CatalogName}: {problems.Count} problema(s).",
                             catalog);
        }

        /// <summary>
        /// Só reconstrói quando algum .asset foi tocado — importar um PNG não
        /// tem por que sujar o catálogo.
        /// </summary>
        public static bool TouchesAssets(params string[][] pathSets)
        {
            foreach (string[] paths in pathSets)
            {
                if (paths == null) continue;
                foreach (string path in paths)
                    if (path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }
}
