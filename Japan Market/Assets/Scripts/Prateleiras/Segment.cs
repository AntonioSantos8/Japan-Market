
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

 [SerializeField] Material greenMaterial, redMaterial, transparentMaterial;
 MeshRenderer meshRenderer;
    private void Start()
    {
        for (int i = 0; i < groups.Length; i++)
            groups[i].Init();

        meshRenderer = GetComponent<MeshRenderer>();
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
    bool TakeItem(ItemBox box)
    {
        for (int g = 0; g < groups.Length; g++)
        {
            if (!box.CanReceive(groups[g].type)) continue;
         
            for (int i = groups[g].spaces.Count - 1; i >= 0; i--)
            {
              
                Transform item = groups[g].spaces[i];

                if (item == null) continue;
           
                int boxIndex = box.GetNullSpace();
                if (boxIndex == -1) return false;
           
                box.GetItems()[boxIndex] = item;

                item.SetParent(box.GetItemsParent());
                item.localPosition = box.spaces[boxIndex].localPosition;
                item.localRotation = box.spaces[boxIndex].transform.localRotation;

                groups[g].spaces[i] = null;
                TakeItem(box);
                return true;
            }
        
        }

        return false;
    }
    public override void Interact()
    {
        if (ServiceLocator.Get<ItemRaycastController>().isWithBox)
        {
            ItemBox box = ServiceLocator.Get<ItemRaycastController>().LastBox();

            if (box.IsEmpty())
            {
                TakeItem(box);
                return;
            }

          
            Transform[] boxItems = box.GetItems();

            for (int i = boxItems.Length - 1; i >= 0; i--)
            {
                if (boxItems[i] == null) continue;

                Item itemInBox = boxItems[i].GetComponent<Item>();

                if (PlaceSingleItem(boxItems[i], itemInBox.GetItemType()))
                {
                    boxItems[i].parent = null;
                    box.RemoveItem(i);
                }
            }
        }
    }
    public override void OnLookAt()
    {
        if (!ServiceLocator.Get<ItemRaycastController>().isWithBox) return;

        ItemBox box = ServiceLocator.Get<ItemRaycastController>().LastBox();

        if (box.IsEmpty())
        {
            meshRenderer.material = redMaterial;
        }
        else
        {
            meshRenderer.material = greenMaterial;
        }
    }
    public override void OnLookAway()
    {
      
       meshRenderer.material = transparentMaterial;
    }
}