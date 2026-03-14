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
public bool isAnimating;
int activeTweens;
float visualDelay;
[SerializeField] float delayBetweenItems = 0.08f;
 [SerializeField] Material greenMaterial, redMaterial, transparentMaterial;
[SerializeField] Transform paiDeTodos;
 MeshRenderer meshRenderer;
 bool isLooking;
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
             


                }

                
            }


        }
  
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

        itemTransform.SetParent(target.parent);

        groups[g].spaces[spaceIndex] = itemTransform;

        Vector3 start = itemTransform.position;
Vector3 end = target.position;

float height = Vector3.Distance(start, end) * 0.21f;

Vector3 mid = (start + end) * 0.5f;
mid += Vector3.up * height;

Vector3[] path = new Vector3[]
{
    start,
    mid,
    end
};

Sequence seq = DOTween.Sequence();

seq.SetDelay(visualDelay + Random.Range(0f,0.025f));

seq.Append(
    itemTransform.DOPath(path, 0.34f, PathType.CatmullRom)
    .SetEase(Ease.OutCubic)
);

seq.Join(
    itemTransform.DORotateQuaternion(
        target.rotation * Quaternion.Euler(
            Random.Range(-6f,6f),
            Random.Range(-12f,12f),
            Random.Range(-4f,4f)
        ),
        0.26f
    ).SetEase(Ease.OutSine)
);

seq.Join(
    itemTransform.DOScale(target.localScale * 1.12f, 0.18f)
    .SetEase(Ease.OutQuad)
);

seq.Append(
    itemTransform.DOMove(end, 0.05f)
    .SetEase(Ease.InQuad)
);

seq.Join(
    itemTransform.DORotateQuaternion(target.rotation, 0.05f)
);

seq.Append(
    itemTransform.DOScale(target.localScale, 0.12f)
    .SetEase(Ease.OutBack)
);

seq.Append(
    itemTransform.DOPunchPosition(Vector3.up * 0.02f, 0.09f, 5, 0.8f)
);

        //transform.DOPunchPosition(Vector3.back * 0.015f, 0.12f, 3, 0.6f);

        activeTweens++;
        isAnimating = true;

        seq.OnComplete(() =>
        {
            activeTweens--;

            if(activeTweens <= 0)
            {
                isAnimating = false;
                OnLookAtWithRestriction();
            }
        });

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

                if (!box.AddItem(item, groups[g].type, this)) { mySegment = Items.None; return false; }

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
    if (isAnimating) return;
    if (ServiceLocator.Get<ItemRaycastController>().isWithBox)
    {
        ItemBox box = ServiceLocator.Get<ItemRaycastController>().LastBox();

        if (box.IsEmpty())
{
    isAnimating = true;
    TakeItem(box);
    OnLookAtWithRestriction();
    return;
}
if(box.isAnimating) return;
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
                box.AddItem(item, type, this);
               
                break;
            }
        }
        OnLookAtWithRestriction();
    }
visualDelay = 0;
  
}
    public override void OnLookAt()
    {
        isLooking = true;
if(isAnimating)
    {
        meshRenderer.material = transparentMaterial;
        return;
    }
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

      isLooking = false;
       meshRenderer.material = transparentMaterial;
    }
    public void OnLookAtWithRestriction(){if(isLooking) OnLookAt();}
   
}