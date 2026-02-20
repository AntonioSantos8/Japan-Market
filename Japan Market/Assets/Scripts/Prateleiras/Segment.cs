
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
public class Segment : MonoBehaviour
{
    Items mySegment = Items.None;
    [SerializeField] Transform[] allItems;
    List<Transform> spaces = new List<Transform>();

    private void Start()
    {
        spaces = new List<Transform>(new Transform[allItems.Length]);
    }
    private void OnTriggerEnter(Collider other)
    {
        
        if (other.gameObject.TryGetComponent(out Item item))
        {
            if (mySegment == Items.None || item.GetItemType() == mySegment)
            {
                if (GetNullSpace() == -1) return;
                mySegment = item.GetItemType();
                item.GetComponent<HoldableItem>().enabled = false;
                Destroy(item.GetComponent<Rigidbody>());
                item.transform.position = allItems[GetNullSpace()].transform.position;

                item.transform.rotation = allItems[GetNullSpace()].transform.rotation;
                spaces[GetNullSpace()] = item.transform;
            }
        }else if (other.gameObject.TryGetComponent(out ItemBox itemBox)) 
        {
        
        
        
        
        
        }
    }
    public int GetNullSpace() 
    {
        for (int i = 0; i < spaces.Count; i++)
        {
            if(spaces[i] == null)
            {
                return i;
            }

        }
        return -1;
    
    
    }
}
