using System.Collections.Generic;
using JapanMarket.Data;
using UnityEditor;

namespace JapanMarket.EditorTools
{
    /// <summary>
    /// Mantém o ObjectiveCatalog em dia com os assets do projeto, e garante que
    /// todo objetivo nasça com um id.
    ///
    /// O id é gerado AQUI e não no <c>OnEnable</c> do asset por um motivo
    /// concreto: duplicar um asset no Project (Ctrl+D, que é como qualquer um
    /// cria o segundo objetivo) copia o id junto. Gerar só quando o campo está
    /// vazio deixaria os dois com o mesmo id, e o save daria o progresso de um
    /// ao outro. O validador do catálogo reclama de id repetido por isso.
    /// </summary>
    public sealed class ObjectiveCatalogPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!CatalogSync.TouchesAssets(imported, deleted, moved)) return;
            SyncNow(logWhenUnchanged: false);
        }

        [MenuItem("Japan Market/Catálogo/Reconstruir objetivos", priority = 104)]
        public static void RebuildFromMenu() => SyncNow(logWhenUnchanged: true);

        [MenuItem("Japan Market/Catálogo/Validar objetivos", priority = 105)]
        public static void ValidateFromMenu() => CatalogSync.Validate<ObjectiveCatalog>();

        private static void SyncNow(bool logWhenUnchanged)
        {
            EnsureIds();
            CatalogSync.Sync<ObjectiveCatalog, ObjectiveDefinition>(
                logWhenUnchanged, CompareForDisplay);
        }

        /// <summary>
        /// Dá id a quem ainda não tem. Roda antes da sincronização porque o
        /// catálogo indexa por id, e um asset sem id ficaria de fora do índice
        /// sem nada apontar o motivo.
        /// </summary>
        private static void EnsureIds()
        {
            List<ObjectiveDefinition> objectives =
                CatalogSync.LoadAllDefinitions<ObjectiveDefinition>();

            for (int i = 0; i < objectives.Count; i++)
            {
                if (!objectives[i].EditorEnsureId()) continue;

                EditorUtility.SetDirty(objectives[i]);
                AssetDatabase.SaveAssetIfDirty(objectives[i]);
            }
        }

        /// <summary>Ordem de catálogo: a que o designer escreveu, nome como desempate.</summary>
        private static int CompareForDisplay(ObjectiveDefinition a, ObjectiveDefinition b) =>
            a.SortOrder != b.SortOrder
                ? a.SortOrder.CompareTo(b.SortOrder)
                : string.CompareOrdinal(a.name, b.name);
    }
}
