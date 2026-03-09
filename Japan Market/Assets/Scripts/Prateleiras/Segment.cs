
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
public class Segment : InteractableBase
{
    [SerializeField] SegmentTypeGroup[] groups;
    bool canPut = true;
    public void SetCanPut(bool value) { canPut = value; }
Items mySegment = Items.None;

 [SerializeField] Material greenMaterial, redMaterial;
 MeshRenderer meshRenderer;
    private void Start()
    {
        for (int i = 0; i < groups.Length; i++)
            groups[i].Init();

        meshRenderer = GetComponent<MeshRenderer>();
    }

    private void OnTriggerEnter(Collider other)
    {if (!canPut) return;
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
    public void FreeSpace(int groupIndex, int spaceIndex)
    {
        groups[groupIndex].spaces[spaceIndex] = null;
 
    }
    bool PlaceSingleItem(Transform itemTransform, Items type)
    {
	
	if(mySegment != Items.None && type != mySegment) { return false;};
        mySegment = type;
        for (int g = 0; g < groups.Length; g++)
        {

            if (groups[g].type != type) continue;
		
            int spaceIndex = groups[g].GetNullSpace();
            if (spaceIndex == -1) {return false;}
	
 itemTransform.gameObject.layer = LayerMask.NameToLayer("InShelf");  

            if (itemTransform.TryGetComponent(out Rigidbody rb))
            {
                if (rb.isKinematic == true) return false;
                rb.isKinematic = true;
            }

	
            itemTransform.position = groups[g].allItems[spaceIndex].position;
            itemTransform.rotation = groups[g].allItems[spaceIndex].rotation;

            groups[g].spaces[spaceIndex] = itemTransform;

            ShelfItem shelfItem = itemTransform.GetComponent<ShelfItem>();
            if (shelfItem == null)
                shelfItem = itemTransform.gameObject.AddComponent<ShelfItem>();

            shelfItem.Setup(this, g, spaceIndex);

            return true;
        }
	
        return false;
    }

    public override void Interact()
    {
         if(ServiceLocator.Get<ItemRaycastController>().isWithBox)
        {
            
 Transform[] boxItems = ServiceLocator.Get<ItemRaycastController>().box.GetItems();

            for (int i = boxItems.Length - 1; i >= 0; i--)
            {
                if (boxItems[i] == null) continue;

                Item itemInBox = boxItems[i].GetComponent<Item>();

                if (PlaceSingleItem(boxItems[i], itemInBox.GetItemType()))
                {
                    boxItems[i].parent = null;
                    ServiceLocator.Get<ItemRaycastController>().box.RemoveItem(i);
                }
            }
            
        }
    }
    public override void OnLookAt()
    {


       if(ServiceLocator.Get<ItemRaycastController>().isWithBox){meshRenderer.material = greenMaterial; }
      
    }
     public override void OnLookAway()
    {
      
       meshRenderer.material = null;
    }
}