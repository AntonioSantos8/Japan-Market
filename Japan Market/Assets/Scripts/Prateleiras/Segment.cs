
using UnityEngine;
using System.Collections.Generic;
[System.Serializable]
public class SegmentTypeGroup
{
    public Items type;
    public Transform[] allItems;
    public List<Transform> spaces;

    public void Init()
    {
        spaces = new List<Transform>(new Transform[allItems.Length]);
    }

    public int GetNullSpace()
    {
        for (int i = 0; i < spaces.Count; i++)
        {
            if (spaces[i] == null)
                return i;
        }
        return -1;
    }
}
public class Segment : MonoBehaviour
{
    [SerializeField] SegmentTypeGroup[] groups;

    private void Start()
    {
        for (int i = 0; i < groups.Length; i++)
            groups[i].Init();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out Item item))
        {
            PlaceSingleItem(item.transform, item.GetItemType());
        }
        else if (other.TryGetComponent(out ItemBox itemBox))
        {
            Transform[] boxItems = itemBox.GetItems();

            for (int i = boxItems.Length - 1; i >= 0; i--)
            {
                if (boxItems[i] == null) continue;

                Item itemInBox = boxItems[i].GetComponent<Item>();

                if (PlaceSingleItem(boxItems[i], itemInBox.GetItemType()))
                {
                    boxItems[i].parent = null;
                    itemBox.RemoveItem(i);
                }
            }
        }
    }

    bool PlaceSingleItem(Transform itemTransform, Items type)
    {
        for (int g = 0; g < groups.Length; g++)
        {
            if (groups[g].type != type) continue;

            int spaceIndex = groups[g].GetNullSpace();
            if (spaceIndex == -1) return false;

            itemTransform.GetComponent<HoldableItem>().enabled = false;
            Destroy(itemTransform.GetComponent<Rigidbody>());

            itemTransform.position = groups[g].allItems[spaceIndex].position;
            itemTransform.rotation = groups[g].allItems[spaceIndex].rotation;

            groups[g].spaces[spaceIndex] = itemTransform;

            return true;
        }

        return false;
    }
}