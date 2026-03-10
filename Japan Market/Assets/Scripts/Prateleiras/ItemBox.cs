using UnityEngine;
using System.Collections.Generic;

public class ItemBox : MonoBehaviour
{
    [SerializeField] Items boxType;
    public SegmentTypeGroup[] groups;
    [SerializeField] Transform itemsParent;

    public Transform GetItemsParent() => itemsParent;

    void Start()
    {
        // for (int i = 0; i < groups.Length; i++)
        //     groups[i].Init();
    }

    public bool IsEmpty()
    {
        for (int g = 0; g < groups.Length; g++)
            for (int i = 0; i < groups[g].spaces.Count; i++)
                if (groups[g].spaces[i] != null)
                    return false;

        return true;
    }

    public Items GetBoxType()
    {
        if (IsEmpty()) return Items.None;
        return boxType;
    }

    public void UpdateBoxType(Items newType)
    {
        if (boxType == Items.None)
            boxType = newType;
    }

    public bool CanReceive(Items type)
    {
        if (GetBoxType() == Items.None) return true;
        return boxType == type;
    }

    public bool AddItem(Transform item, Items type)
    {
        for (int g = 0; g < groups.Length; g++)
        {
            if (groups[g].type != type) continue;

            int index = groups[g].GetNullSpace();
            if (index == -1) return false;

            groups[g].spaces[index] = item;

            item.SetParent(itemsParent);
            item.position = groups[g].allItems[index].position;
            item.rotation = groups[g].allItems[index].rotation;

            if (item.TryGetComponent(out Rigidbody rb))
                rb.isKinematic = true;

            UpdateBoxType(type);
            return true;
        }

        return false;
    }

    public Transform TakeItemByType(Items type)
    {
        for (int g = 0; g < groups.Length; g++)
        {
            if (groups[g].type != type) continue;

            for (int i = groups[g].spaces.Count - 1; i >= 0; i--)
            {
                Transform item = groups[g].spaces[i];
                if (item == null) continue;

                groups[g].spaces[i] = null;

                if (IsEmpty())
                    boxType = Items.None;

                return item;
            }
        }

        return null;
    }
}