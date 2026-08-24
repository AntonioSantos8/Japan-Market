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
    [SerializeField] private AllIThingsData[] allItemsData;

    private Dictionary<Items, AllIThingsData> dataLookup;

    private void Awake()
    {
        ServiceLocator.Register(this);

        dataLookup = new Dictionary<Items, AllIThingsData>();
        foreach (var data in allItemsData)
        {
            if (data == null) continue;
            if (!dataLookup.ContainsKey(data.itemType))
                dataLookup.Add(data.itemType, data);
        }
    }

    public Sprite GetItemIcon(Items type)
    {
        var found = itemVisuals.Find(x => x.type == type);
        return found != null ? found.icon : null;
    }

    public AllIThingsData GetItemData(Items type)
    {
        dataLookup.TryGetValue(type, out AllIThingsData data);
        return data;
    }
}
