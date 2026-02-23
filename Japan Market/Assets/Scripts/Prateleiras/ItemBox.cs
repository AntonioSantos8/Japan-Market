using System.Linq;
using UnityEngine;

public class ItemBox : MonoBehaviour
{
    [SerializeField]Items boxType;
    [SerializeField] Transform[] spaces, items;
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
        for (int i = 0; i < spaces.Length; i++)
        {
            if (spaces[i] == null)
            {
                return i;
            }

        }
        return -1;




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
