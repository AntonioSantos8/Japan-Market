using UnityEngine;
using System.Collections.Generic;

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
        if (_itemDataMap.TryGetValue(type, out var data))
            return data;
        return null;
    }

    public List<AllIThingsData> GetAllItemsData()
    {
        return allItemsData;
    }

    public List<AllIThingsData> GetItemsForFurniture(FurnitureType furniture)
    {
        List<AllIThingsData> result = new List<AllIThingsData>();
        if (allItemsData != null)
        {
            foreach (var data in allItemsData)
            {
                if (data != null && data.allowedFurniture == furniture)
                    result.Add(data);
            }
        }
        return result;
    }

    public Sprite GetItemIcon(Items type)
    {
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