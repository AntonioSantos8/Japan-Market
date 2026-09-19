using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JapanMarket.Core;
using JapanMarket.Data;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Migra os AllIThingsData legados para ItemDefinition.
///
/// Este arquivo mora em Assets/Editor/ SEM assembly definition de propósito: só
/// assim ele compila dentro de Assembly-CSharp-Editor, que enxerga tanto o código
/// legado (AllIThingsData, enum Items, FurnitureData) quanto os assemblies novos.
/// Um script dentro de JapanMarket.Editor não conseguiria ver o legado, porque a
/// direção de dependência proíbe — que é exatamente o ponto da arquitetura.
///
/// Ele sai do projeto junto com o legado, na Fase 8.
///
/// USO — nesta ordem:
///   1. Japan Market → Migração → 1 · Analisar (dry-run)
///      Não escreve nada. Lista o que seria criado e o que precisa de decisão humana.
///   2. Japan Market → Migração → 2 · Gerar ItemDefinitions
///      Cria os assets. Idempotente: rodar de novo atualiza, não duplica.
///
/// O QUE ELE AINDA NÃO FAZ: reescrever as referências de enum dentro de cenas e
/// prefabs. Esse é o passo destrutivo e fica para uma ferramenta separada, depois
/// que você tiver conferido os assets gerados aqui.
/// </summary>
public static class LegacyItemsMigrator
{
    private const string GeneratedFolder = "Assets/JapanMarket/Data/Products/Generated";
    private const string TraitsFolder    = "Assets/JapanMarket/Data/Products/StorageTraits";
    private const string CategoryFolder  = "Assets/JapanMarket/Data/Products/Categories";
    private const string ReportPath      = "Assets/JapanMarket/Data/MIGRACAO_RELATORIO.txt";

    // ─────────────────────────────────────────────────────────────────────────

    [MenuItem("Japan Market/Migração/1 · Analisar (dry-run)", priority = 200)]
    public static void Analyze() => Run(dryRun: true);

    [MenuItem("Japan Market/Migração/2 · Gerar ItemDefinitions", priority = 201)]
    public static void Migrate()
    {
        bool ok = EditorUtility.DisplayDialog(
            "Gerar ItemDefinitions",
            "Isto cria assets novos em:\n" + GeneratedFolder +
            "\n\nNenhum asset legado é apagado ou modificado, e nenhuma cena é tocada.\n\n" +
            "Rodou o dry-run antes?",
            "Gerar", "Cancelar");

        if (ok) Run(dryRun: false);
    }

    // ─────────────────────────────────────────────────────────────────────────

    private sealed class Row
    {
        public AllIThingsData Source;
        public string SourcePath;
        public string Verdict;         // "produto", "móvel", "expansão", "ignorado"
        public string Note;
        public long BoxCost;
        public int  UnitsPerBox;
        public long UnitCost;
        public long MarketPrice;
    }

    private static void Run(bool dryRun)
    {
        List<AllIThingsData> sources = LoadAll<AllIThingsData>();
        if (sources.Count == 0)
        {
            Debug.LogWarning("[Migração] Nenhum AllIThingsData encontrado no projeto.");
            return;
        }

        HashSet<AllIThingsData> usedByFurniture = CollectFurnitureData();
        var rows = new List<Row>();

        foreach (AllIThingsData source in sources)
        {
            string path = AssetDatabase.GetAssetPath(source);
            var row = new Row { Source = source, SourcePath = path };

            if (path.Contains("/Expansion/"))
            {
                row.Verdict = "expansão";
                row.Note = "Vira ExpansionDefinition na Fase 7 — não é produto.";
            }
            else if (usedByFurniture.Contains(source))
            {
                row.Verdict = "móvel";
                row.Note = "Referenciado por um FurnitureData — vira FurnitureDefinition na Fase 3.";
            }
            else if (IsFurnitureLikeEnum(source.itemType))
            {
                row.Verdict = "ignorado";
                row.Note = $"itemType '{source.itemType}' é móvel/embalagem dentro do enum de produtos. " +
                           "Confira à mão antes de decidir.";
            }
            else
            {
                row.Verdict = "produto";
                MeasurePrices(source, row);
            }

            rows.Add(row);
        }

        string report = BuildReport(rows, dryRun);
        Debug.Log(report);

        if (dryRun) { WriteReport(report); return; }

        // ── escrita ──────────────────────────────────────────────────────────
        EnsureFolder(GeneratedFolder);
        EnsureFolder(TraitsFolder);
        EnsureFolder(CategoryFolder);

        // ATENÇÃO: tudo o que precisa ser LIDO de volta depois de criado tem que
        // nascer FORA do StartAssetEditing. Dentro do batch, LoadAssetAtPath não
        // enxerga assets criados no mesmo batch — e CreateAsset apaga o que houver
        // no caminho. O resultado seria um trait recriado a cada produto, deixando
        // todas as referências anteriores apontando para objetos destruídos.
        ProductCategory uncategorized = GetOrCreate<ProductCategory>(
            CategoryFolder, "Sem categoria");

        var traits = new Dictionary<FurnitureType, StorageTrait>();
        foreach (Row row in rows)
        {
            if (row.Verdict != "produto") continue;
            FurnitureType key = row.Source.allowedFurniture;
            if (!traits.ContainsKey(key))
                traits.Add(key, GetOrCreate<StorageTrait>(TraitsFolder, TraitNameFor(key)));
        }

        AssetDatabase.SaveAssets();

        int created = 0, updated = 0;

        // Mesmo motivo: se dois AllIThingsData tiverem o mesmo itemName, o segundo
        // sobrescreveria o primeiro em silêncio, porque LoadAssetAtPath devolveria
        // null dentro do batch e isNew voltaria a ser true.
        var writtenPaths = new HashSet<string>();

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (Row row in rows)
            {
                if (row.Verdict != "produto") continue;

                string assetName = SanitizeName(row.Source.itemName, row.Source.name);
                string assetPath = UniquePath(writtenPaths, assetName, row);

                var definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(assetPath);
                bool isNew = definition == null;

                if (isNew)
                {
                    definition = ScriptableObject.CreateInstance<ItemDefinition>();
                    AssetDatabase.CreateAsset(definition, assetPath);
                    created++;
                }
                else updated++;

                Apply(definition, row, uncategorized, traits, isNew);
                EditorUtility.SetDirty(definition);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string done = $"[Migração] {created} ItemDefinition criados, {updated} atualizados. " +
                      "Agora rode: Japan Market → Catálogo → Reconstruir catálogo, " +
                      "e depois → Validar catálogo.";
        Debug.Log(done);
        WriteReport(report + "\n\n" + done);
    }

    // ── escrita de um produto ────────────────────────────────────────────────

    private static void Apply(ItemDefinition target, Row row,
                              ProductCategory fallbackCategory,
                              Dictionary<FurnitureType, StorageTrait> traits,
                              bool isNew)
    {
        AllIThingsData src = row.Source;

        if (isNew)
            target.EditorInitialize(ProductId.Generate(), (int)src.itemType);

        // O texto legado é sempre inglês, e o passo 3 do relatório pede que você
        // preencha o português à mão. Reescrever isso a cada execução apagaria
        // exatamente esse trabalho — então só escrevemos o que ainda está vazio.
        LocalizedText displayName = target.DisplayName.IsEmpty
            ? LocalizedText.FromSingle(GameLanguage.English, src.itemName)
            : target.DisplayName;

        LocalizedText description = target.Description.IsEmpty
            ? LocalizedText.FromSingle(GameLanguage.English, src.description)
            : target.Description;

        target.EditorSetContent(
            displayName, description,
            Money.FromYen(row.UnitCost),
            Money.FromYen(row.MarketPrice),
            src.itemPrefab, src.itemBoxPrefab, src.itemSprite);

        var so = new SerializedObject(target);

        so.FindProperty("_unitsPerBox").intValue = Mathf.Max(1, row.UnitsPerBox);

        SerializedProperty categoryProp = so.FindProperty("_category");
        if (categoryProp.objectReferenceValue == null)
            categoryProp.objectReferenceValue = fallbackCategory;

        SerializedProperty storageProp = so.FindProperty("_requiredStorage");
        if (storageProp.objectReferenceValue == null &&
            traits.TryGetValue(src.allowedFurniture, out StorageTrait trait))
            storageProp.objectReferenceValue = trait;

        CopyGrid(so.FindProperty("_shelfGrid"), src.shelfGrid);
        CopyGrid(so.FindProperty("_boxGrid"),   src.boxGrid);

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Evita que dois produtos de mesmo nome escrevam no mesmo arquivo.</summary>
    private static string UniquePath(HashSet<string> taken, string assetName, Row row)
    {
        string path = $"{GeneratedFolder}/{assetName}.asset";
        if (taken.Add(path)) return path;

        for (int suffix = 2; suffix < 100; suffix++)
        {
            string candidate = $"{GeneratedFolder}/{assetName} ({suffix}).asset";
            if (!taken.Add(candidate)) continue;

            row.Note = (row.Note == null ? "" : row.Note + " ") +
                       $"Nome duplicado no legado — salvo como '{assetName} ({suffix})'.";
            return candidate;
        }

        return path;
    }

    private static void CopyGrid(SerializedProperty target, ItemGridSettings source)
    {
        if (target == null || source == null) return;

        target.FindPropertyRelative("_count").vector3IntValue      = source.count;
        target.FindPropertyRelative("_spacing").vector3Value       = source.spacing;
        target.FindPropertyRelative("_originOffset").vector3Value  = source.originOffset;
        target.FindPropertyRelative("_itemRotation").vector3Value  = source.itemRotation;
        target.FindPropertyRelative("_itemScale").vector3Value     = source.itemScale;
    }

    /// <summary>
    /// singleItemPrice é o preço de uma CAIXA (é assim que ShopBuyItems.BuyBox
    /// cobra), enquanto marketPrice é a referência por unidade. O custo unitário
    /// sai da divisão pela capacidade da caixa — o relatório mostra as três
    /// contas para você conferir antes de aceitar.
    /// </summary>
    private static void MeasurePrices(AllIThingsData source, Row row)
    {
        int capacity = source.boxGrid != null ? source.boxGrid.TotalCapacity : 0;
        if (capacity <= 0) capacity = 1;

        // Money.FromYen(double) arredonda meio-para-cima; Mathf.RoundToInt usa
        // arredondamento bancário. Misturar os dois faria os preços migrados
        // divergirem de todo cálculo posterior nos valores terminados em .5.
        row.BoxCost     = Money.FromYen((double)source.singleItemPrice).Yen;
        row.UnitsPerBox = capacity;
        row.UnitCost    = Money.FromYen((double)source.singleItemPrice / capacity).Yen;
        row.MarketPrice = Money.FromYen((double)source.marketPrice).Yen;

        if (row.MarketPrice <= row.UnitCost)
            row.Note = $"Margem negativa ou zero: custo ¥{row.UnitCost} vs mercado ¥{row.MarketPrice}.";
    }

    private static string TraitNameFor(FurnitureType allowed) => allowed switch
    {
        FurnitureType.Freezer => "Congelado",
        FurnitureType.Counter => "Balcão",
        _                     => "Ambiente",
    };

    // ── relatório ────────────────────────────────────────────────────────────

    private static string BuildReport(List<Row> rows, bool dryRun)
    {
        var sb = new StringBuilder();
        sb.AppendLine(dryRun
            ? "═══ MIGRAÇÃO — DRY RUN (nada foi escrito) ═══"
            : "═══ MIGRAÇÃO — EXECUTADA ═══");
        sb.AppendLine();

        foreach (string verdict in new[] { "produto", "móvel", "expansão", "ignorado" })
        {
            List<Row> group = rows.Where(r => r.Verdict == verdict).ToList();
            if (group.Count == 0) continue;

            sb.AppendLine($"── {verdict.ToUpperInvariant()} ({group.Count}) ──");

            foreach (Row row in group)
            {
                sb.Append("  • ").Append(row.Source.itemName);

                if (verdict == "produto")
                {
                    sb.Append($"   caixa ¥{row.BoxCost} ÷ {row.UnitsPerBox} un = ")
                      .Append($"custo ¥{row.UnitCost}/un · mercado ¥{row.MarketPrice}/un");
                }

                if (!string.IsNullOrEmpty(row.Note)) sb.Append("\n      ⚠ ").Append(row.Note);
                sb.AppendLine();
            }
            sb.AppendLine();
        }

        sb.AppendLine("── CONFERIR À MÃO DEPOIS ──");
        sb.AppendLine("  1. Categorias: todos entram como 'Sem categoria'. O legado não tinha esse dado.");
        sb.AppendLine("  2. Custo unitário é inferido (preço da caixa ÷ capacidade). Confira os números acima.");
        sb.AppendLine("  3. Nomes e descrições entram como inglês. Preencha o português no asset.");
        sb.AppendLine("  4. Traits de armazenamento vêm de 'allowedFurniture' — confira os congelados.");
        sb.AppendLine();
        sb.AppendLine("Nenhum asset legado foi apagado. Nenhuma cena foi tocada.");

        return sb.ToString();
    }

    private static void WriteReport(string report)
    {
        try
        {
            EnsureFolder(Path.GetDirectoryName(ReportPath).Replace('\\', '/'));
            File.WriteAllText(ReportPath, report, Encoding.UTF8);
            AssetDatabase.ImportAsset(ReportPath);
            Debug.Log($"[Migração] Relatório salvo em {ReportPath}");
        }
        catch (IOException e)
        {
            Debug.LogWarning($"[Migração] Não consegui salvar o relatório: {e.Message}");
        }
    }

    // ── utilitários ──────────────────────────────────────────────────────────

    private static bool IsFurnitureLikeEnum(Items type) =>
        type == Items.Shelf || type == Items.Freezer || type == Items.Box || type == Items.None;

    private static HashSet<AllIThingsData> CollectFurnitureData()
    {
        var used = new HashSet<AllIThingsData>();
        foreach (FurnitureData furniture in LoadAll<FurnitureData>())
            if (furniture.data != null) used.Add(furniture.data);
        return used;
    }

    private static List<T> LoadAll<T>() where T : Object =>
        AssetDatabase.FindAssets($"t:{typeof(T).Name}")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(a => a != null)
            .ToList();

    private static T GetOrCreate<T>(string folder, string assetName) where T : ScriptableObject
    {
        string path = $"{folder}/{assetName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;

        EnsureFolder(folder);
        var created = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(created, path);
        return created;
    }

    private static void EnsureFolder(string folder)
    {
        if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;

        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static string SanitizeName(string preferred, string fallback)
    {
        string name = string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;
        foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
        return name.Trim();
    }
}
