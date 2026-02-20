
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
        spaces = new List<Transform>(allItems.Length);
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.TryGetComponent(out Item item))
        {

            item.GetComponent<HoldableItem>().enabled = false;
            Destroy(item.GetComponent<Rigidbody>());
            item.transform.position = allItems[GetNullSpace()].transform.position;

            item.transform.rotation = allItems[GetNullSpace()].transform.rotation;
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
        return 0;
    
    
    }
}
