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

    private void Awake()
    {
        ServiceLocator.Register(this);
    }

    public Sprite GetItemIcon(Items type)
    {
        var found = itemVisuals.Find(x => x.type == type);
        return found != null ? found.icon : null;
    }
}