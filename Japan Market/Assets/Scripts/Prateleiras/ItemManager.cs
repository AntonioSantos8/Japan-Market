using UnityEngine;
using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;

[System.Serializable]
public class ItemVisualData
{
    public Items type;
    public Sprite icon;
}

public class ItemManager : MonoBehaviour
{
    [SerializeField] private List<ItemVisualData> itemVisuals = new List<ItemVisualData>();
    [SerializeField] private List<AllIThingsData> allItemsData = new List<AllIThingsData>();

    private Dictionary<Items, AllIThingsData> _itemDataMap;
    private readonly Dictionary<Items, AllIThingsData> _runtimeData = new();

    private void Awake()
    {
        ServiceLocator.Register(this);
        BuildCache();
    }

    private void BuildCache()
    {
        if (_itemDataMap != null) return;
        _itemDataMap = new Dictionary<Items, AllIThingsData>();
        if (allItemsData != null)
        {
            foreach (var data in allItemsData)
            {
                if (data != null && data.itemType != Items.None && !_itemDataMap.ContainsKey(data.itemType))
                {
                    _itemDataMap.Add(data.itemType, data);
                }
            }
        }
    }

    public AllIThingsData GetItemData(Items type)
    {
        BuildCache();
        _itemDataMap.TryGetValue(type, out var legacy);
        ItemDefinition product = GetItemDefinition(type);
        return product != null ? GetItemData(product) : legacy;
    }

    public ItemDefinition GetItemDefinition(Items type)
    {
        if (type == Items.None || !ServiceContainer.Current.TryResolve(out IItemCatalog catalog))
            return null;

        foreach (ItemDefinition product in catalog.All)
            if (product != null && product.LegacyEnumValue == (int)type) return product;
        return null;
    }

    // Adapt only in memory: existing components keep their enum/API and the
    // original assets remain untouched. Definitions are the runtime authority.
    public AllIThingsData GetItemData(ItemDefinition product)
    {
        if (product == null) return null;
        BuildCache();
        Items type = (Items)product.LegacyEnumValue;
        _itemDataMap.TryGetValue(type, out var legacy);
        if (!_runtimeData.TryGetValue(type, out var data))
        {
            data = ScriptableObject.CreateInstance<AllIThingsData>();
            data.hideFlags = HideFlags.HideAndDontSave;
            _runtimeData.Add(type, data);
        }

        data.name = product.name;
        data.itemType = type;
        data.itemName = !product.DisplayName.IsEmpty ? product.DisplayName.Value
            : legacy != null ? legacy.itemName : product.name;
        data.description = !product.Description.IsEmpty ? product.Description.Value
            : legacy != null ? legacy.description : string.Empty;
        // The legacy field denotes a BOX price; BaseCost denotes a UNIT cost.
        data.singleItemPrice = (float)product.BoxCost.Yen;
        data.marketPrice = (float)product.MarketPrice.Yen;
        data.itemPrefab = product.ItemPrefab != null ? product.ItemPrefab : legacy?.itemPrefab;
        data.itemBoxPrefab = product.BoxPrefab != null ? product.BoxPrefab : legacy?.itemBoxPrefab;
        data.itemSprite = product.Icon != null ? product.Icon : legacy?.itemSprite;
        data.allowedFurniture = ResolveFurniture(product, legacy);
        CopyGrid(product.ShelfGrid, data.shelfGrid);
        CopyGrid(product.BoxGrid, data.boxGrid);
        return data;
    }

    private static void CopyGrid(ItemGrid source, ItemGridSettings target)
    {
        target.count = source.Count;
        target.spacing = source.Spacing;
        target.originOffset = source.OriginOffset;
        target.itemRotation = source.Rotation.eulerAngles;
        target.itemScale = source.ItemScale;
    }

    private static FurnitureType ResolveFurniture(ItemDefinition product, AllIThingsData legacy)
    {
        // Names assigned by LegacyItemsMigrator. Custom traits retain the
        // existing furniture restriction until the enum-based flow is retired.
        if (product.RequiredStorage == null) return FurnitureType.None;
        return product.RequiredStorage.name switch
        {
            "Ambiente" => FurnitureType.Shelf,
            "Congelado" => FurnitureType.Freezer,
            "Balcão" => FurnitureType.Counter,
            _ => legacy != null ? legacy.allowedFurniture : FurnitureType.None
        };
    }

    private void OnDestroy()
    {
        foreach (AllIThingsData data in _runtimeData.Values)
            if (data != null) Destroy(data);
        _runtimeData.Clear();
    }

    public List<AllIThingsData> GetAllItemsData()
    {
        BuildCache();
        var result = new List<AllIThingsData>();
        var added = new HashSet<Items>();
        if (allItemsData != null)
            foreach (AllIThingsData legacy in allItemsData)
                if (legacy != null && added.Add(legacy.itemType))
                    result.Add(GetItemData(legacy.itemType));

        if (ServiceContainer.Current.TryResolve(out IItemCatalog catalog))
            foreach (ItemDefinition product in catalog.All)
                if (product != null && product.LegacyEnumValue > 0 &&
                    added.Add((Items)product.LegacyEnumValue))
                    result.Add(GetItemData(product));
        return result;
    }

    public List<AllIThingsData> GetItemsForFurniture(FurnitureType furniture)
    {
        List<AllIThingsData> result = new List<AllIThingsData>();
        foreach (var data in GetAllItemsData())
        {
            if (data != null && (data.allowedFurniture == furniture ||
                (data.allowedFurniture == FurnitureType.None && GetItemDefinition(data.itemType) != null)))
                result.Add(data);
        }
        return result;
    }

    public Sprite GetItemIcon(Items type)
    {
        ItemDefinition product = GetItemDefinition(type);
        if (product != null && product.Icon != null) return product.Icon;
        var found = itemVisuals.Find(x => x.type == type);
        if (found != null && found.icon != null)
            return found.icon;

        var data = GetItemData(type);
        return data != null ? data.itemSprite : null;
    }

#if UNITY_EDITOR
    [ContextMenu("Auto Populate All Items Data")]
    public void AutoPopulateAllItemsData()
    {
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:AllIThingsData");
        allItemsData.Clear();
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var data = UnityEditor.AssetDatabase.LoadAssetAtPath<AllIThingsData>(path);
            if (data != null && !allItemsData.Contains(data))
                allItemsData.Add(data);
        }
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
