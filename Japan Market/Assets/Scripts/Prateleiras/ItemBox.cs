using System.Linq;
using UnityEngine;

public class ItemBox : MonoBehaviour
{
    [SerializeField]Items boxType;
    [SerializeField] public Transform[] spaces, items;
    [SerializeField] Transform itemsParent;
    public Transform GetItemsParent() => itemsParent;
    public Transform GetLastItem() 
    {
        return items[GetLastSpace()];


    }
    public void RemoveItem(int index)
    {
        items[index] = null;
    }
    public Transform[] GetItems() 
    {
        return items;
    
    }
    public int GetNullSpace()
    {
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null)
            {
                return i;
            }

        }
        return -1;




    }
    public bool IsEmpty()
    {
   
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null) return false;
        }
        return true;
    }

    public bool CanReceive(Items type)
    {
        return boxType == type;
    }

    public bool AddItem(Transform item)
    {
        int index = GetNullSpace();
        if (index == -1) return false;

        items[index] = item;
        item.SetParent(spaces[index]);
        item.localPosition = Vector3.zero;
        item.localRotation = Quaternion.identity;

        if (item.TryGetComponent(out Rigidbody rb))
        {
            rb.isKinematic = true;
        }

        return true;
    }
    public int GetLastSpace()
    {
        for (int i = spaces.Length -1; i != 0; i++)
        {
            if (spaces[i] != null)
            {
                return i;
            }

        }
        return -1;




    }
}
