

using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
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
     public void InitWithType(Items item)
    {
        if(item!= type)
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
[SerializeField] Shelf shelf;
float visualDelay;
[SerializeField] float delayBetweenItems = 0.08f;
 [SerializeField] Material greenMaterial, redMaterial, transparentMaterial;
[SerializeField] Transform paiDeTodos;
 MeshRenderer meshRenderer;
    private void Start()
    {
        for (int i = 0; i < groups.Length; i++)
            groups[i].Init();

        meshRenderer = GetComponent<MeshRenderer>();
    }

 public bool IsFull()
    {
        if(mySegment == Items.None) return false;

        foreach(SegmentTypeGroup sT in groups)
        {
            if(sT.type == mySegment)
            {
               
                    for(int i = 0; i < sT.spaces.Count; i++)
                {
                  
                    if(sT.spaces[i] == null) return false;
                    print(sT.spaces[i]);


                }

                
            }


        }
        print("chegou totalmente ao fim");
            return true;


    
     
    }

    
    public void FreeSpace(int groupIndex, int spaceIndex)
    {   
        groups[groupIndex].spaces[spaceIndex] = null;
 
    }
 bool PlaceSingleItem(Transform itemTransform, Items type)
{
    if(mySegment != Items.None && type != mySegment) return false;

    mySegment = type;

    for (int g = 0; g < groups.Length; g++)
    {
        if (groups[g].type != type) continue;

        int spaceIndex = groups[g].GetNullSpace();
        if (spaceIndex == -1) return false;

        Transform target = groups[g].allItems[spaceIndex];

        itemTransform.SetParent(target.transform.parent);

        groups[g].spaces[spaceIndex] = itemTransform;

        Sequence seq = DOTween.Sequence();

        seq.SetDelay(visualDelay);

        seq.Append(
            itemTransform.DOMove(target.position, 0.25f).SetEase(Ease.OutCubic)
        );

        seq.Join(
            itemTransform.DORotateQuaternion(target.rotation, 0.2f)
        );

        seq.Join(
            itemTransform.DOScale(target.localScale, 0.25f).SetEase(Ease.OutCubic)
        );

       // seq.Append(
            //itemTransform.DOPunchScale(Vector3.one * 0.15f, 0.15f, 6, 0.8f)
        //);

        visualDelay += delayBetweenItems;

        ShelfItem shelfItem = itemTransform.GetComponent<ShelfItem>();
        if (shelfItem == null)
            shelfItem = itemTransform.gameObject.AddComponent<ShelfItem>();

        shelf.RegisterSegment(type, this);
        shelfItem.Setup(this, g, spaceIndex);

        return true;
    }

    return false;
}
   bool TakeItem(ItemBox box)
{//colocar item na caixa
    for (int g = 0; g < groups.Length; g++)
    {
        if (!box.CanReceive(groups[g].type)) continue;

        for (int i = groups[g].spaces.Count - 1; i >= 0; i--)
        {
            Transform item = groups[g].spaces[i];
            if (item == null) continue;

                if (!box.AddItem(item, groups[g].type)) { mySegment = Items.None; return false; }

            groups[g].spaces[i] = null;

            TakeItem(box);
            return true;
        }
    }
    mySegment = Items.None;
    shelf.RemoveSegment(this);
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
             OnLookAt();
            return;
        }

        Items type = box.GetBoxType();

        while (true)
        {
            Transform item = box.TakeItemByType(type);
            if (item == null)
             {  
                box.SetBoxType(Items.None);
                break;
                }

            Item itemComponent = item.GetComponent<Item>();

            if (!PlaceSingleItem(item, itemComponent.GetItemType()))
            {
                box.AddItem(item, type);
               
                break;
            }
        }
        OnLookAt();
    }
visualDelay = 0;
  
}
    public override void OnLookAt()
    {
        if (!ServiceLocator.Get<ItemRaycastController>().isWithBox) return;
         ItemBox box = ServiceLocator.Get<ItemRaycastController>().LastBox();
         if(box.IsEmpty() && mySegment == Items.None) return;
       // if (mySegment != Items.None && mySegment != box.GetBoxType() && !box.IsEmpty()) return;
        if(box.GetBoxType() != mySegment && box.GetBoxType() != Items.None && mySegment != Items.None) return;
       
        if(IsFull() && box.GetBoxType() != Items.None) return;
      
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
      if(meshRenderer == null) return;  
       meshRenderer.material = transparentMaterial;
    }
}