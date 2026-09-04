using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
[System.Serializable]
public class SegmentTypeGroup
{
    public Items type;
    public List<Transform> spaces = new List<Transform>();
    [System.NonSerialized] public ItemGridSettings gridSettings;

    public void Init(ItemGridSettings settings)
    {
        gridSettings = settings;
        int capacity = settings != null ? settings.TotalCapacity : 0;
        spaces = new List<Transform>(new Transform[capacity]);
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
    [SerializeField] List<Items> supportedItems = new List<Items>();
    [SerializeField] Transform itemsParent;
    [SerializeField] float delayBetweenItems = 0.08f;
    [SerializeField] Material greenMaterial, redMaterial, transparentMaterial;
    [SerializeField] Shelf shelf;
    [SerializeField] FurnitureType myType;
    public FurnitureType FurnitureType => myType;

    public SegmentTypeGroup[] Groups 
    { 
        get 
        { 
            InitializeGroups(); 
            return groups; 
        } 
        set => groups = value; 
    }
    public bool IsAnimating { set => isAnimating = value; }

    [SerializeField] MeshRenderer outlineMeshRenderer;

    int activeTweens;
    float visualDelay;
    bool isLooking;
    bool isAnimating;    

    Items mySegment = Items.None;
    public Items SegmenyType => mySegment;

    Tween materialColorTween;
    Tween outlineWidhtTween;
    Tween outlineColorTween;
    bool _isInitialized;

    public void InitializeGroups()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        EnsureItemsParent();

        var itemManager = ServiceLocator.Get<ItemManager>();

        if (groups != null && groups.Length > 0)
        {
            for (int i = 0; i < groups.Length; i++)
            {
                var data = itemManager != null ? itemManager.GetItemData(groups[i].type) : null;
                groups[i].Init(data != null ? data.shelfGrid : null);
            }
        }
        else if (supportedItems != null && supportedItems.Count > 0)
        {
            groups = new SegmentTypeGroup[supportedItems.Count];
            for (int i = 0; i < supportedItems.Count; i++)
            {
                groups[i] = new SegmentTypeGroup { type = supportedItems[i] };
                var data = itemManager != null ? itemManager.GetItemData(supportedItems[i]) : null;
                groups[i].Init(data != null ? data.shelfGrid : null);
            }
        }
        else if (itemManager != null)
        {
            var matchingData = itemManager.GetItemsForFurniture(myType);
            if (matchingData != null && matchingData.Count > 0)
            {
                groups = new SegmentTypeGroup[matchingData.Count];
                for (int i = 0; i < matchingData.Count; i++)
                {
                    groups[i] = new SegmentTypeGroup { type = matchingData[i].itemType };
                    groups[i].Init(matchingData[i].shelfGrid);
                }
            }
            else
            {
                groups = new SegmentTypeGroup[0];
            }
        }
        else
        {
            groups = new SegmentTypeGroup[0];
        }
    }

    void EnsureItemsParent()
    {
        if (itemsParent != null) return;

        if (transform.parent != null)
        {
            GameObject container = new GameObject($"{gameObject.name}_Items");
            container.transform.SetParent(transform.parent);
            container.transform.position = transform.position;
            container.transform.rotation = transform.rotation;
            container.transform.localScale = Vector3.one;
            itemsParent = container.transform;
        }
        else
        {
            itemsParent = transform;
        }
    }

    private void Start()
    {
        InitializeGroups();

        if (outlineMeshRenderer == null)
            outlineMeshRenderer = GetComponent<MeshRenderer>();

        outline = gameObject.GetComponent<Outline>();
        if (outline != null)
            outline.OutlineWidth = 0;
    }   

    public bool IsEmpty()
    {
        return mySegment == Items.None;
    }

    public void RemoveItem(int groupIndex, int spaceIndex)
    {
        groups[groupIndex].spaces[spaceIndex] = null;

        bool hasAny = false;

        for (int g = 0; g < groups.Length; g++)
        {
            for (int i = 0; i < groups[g].spaces.Count; i++)
            {
                if (groups[g].spaces[i] != null)
                {
                    hasAny = true;
                    break;
                }
            }
            if (hasAny) break;
        }

        if (!hasAny)
        {
            mySegment = Items.None;
        }
    }

    public bool IsFull()
    {
        if (mySegment == Items.None) return false;

        foreach (SegmentTypeGroup sT in groups)
        {
            if (sT.type == mySegment)
            {
                if (sT.spaces.Count == 0) return true;
                for (int i = 0; i < sT.spaces.Count; i++)
                {
                    if (sT.spaces[i] == null) return false;
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
        if (mySegment != Items.None && type != mySegment) return false;

        InitializeGroups();

        int groupIndex = -1;
        for (int g = 0; g < groups.Length; g++)
        {
            if (groups[g].type == type)
            {
                groupIndex = g;
                break;
            }
        }

        // Se o grupo não existia na lista inicial mas o item é suportado por esse tipo de móvel, adiciona em runtime
        if (groupIndex == -1 && (supportedItems == null || supportedItems.Count == 0))
        {
            var itemData = ServiceLocator.Get<ItemManager>()?.GetItemData(type);
            if (itemData != null && (itemData.allowedFurniture == myType || myType == FurnitureType.None))
            {
                var newGroup = new SegmentTypeGroup { type = type };
                newGroup.Init(itemData.shelfGrid);
                System.Array.Resize(ref groups, groups.Length + 1);
                groups[groups.Length - 1] = newGroup;
                groupIndex = groups.Length - 1;
            }
        }

        if (groupIndex == -1) return false;

        int spaceIndex = groups[groupIndex].GetNullSpace();
        if (spaceIndex == -1) return false;

        EnsureItemsParent();

        ItemGridSettings settings = groups[groupIndex].gridSettings;
        if (settings == null)
        {
            var data = ServiceLocator.Get<ItemManager>()?.GetItemData(type);
            settings = data != null ? data.shelfGrid : new ItemGridSettings();
            groups[groupIndex].gridSettings = settings;
        }

        mySegment = type;
        ServiceLocator.Get<GlobalPrices>().HasPutItem(mySegment);

        itemTransform.SetParent(itemsParent);
        groups[groupIndex].spaces[spaceIndex] = itemTransform;

        ServiceLocator.Get<SoundManager>().Play(SFX.WooshTransicaoItem);

        Vector3 localPos = settings.GetLocalPosition(spaceIndex);
        Vector3 end = itemsParent.TransformPoint(localPos);
        Quaternion targetRotation = itemsParent.rotation * settings.GetLocalRotation();
        Vector3 targetScale = settings.itemScale;

        Vector3 start = itemTransform.position;
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

        seq.SetDelay(visualDelay + Random.Range(0f, 0.025f));

        seq.Append(
            itemTransform.DOPath(path, 0.34f, PathType.CatmullRom)
            .SetEase(Ease.OutCubic)
        );

        seq.Join(
            itemTransform.DORotateQuaternion(
                targetRotation * Quaternion.Euler(
                    Random.Range(-6f, 6f),
                    Random.Range(-12f, 12f),
                    Random.Range(-4f, 4f)
                ),
                0.26f
            ).SetEase(Ease.OutSine)
        );

        seq.Append(
            itemTransform.DOMove(end, 0.05f)
            .SetEase(Ease.InQuad)
        );

        seq.Join(
            itemTransform.DORotateQuaternion(targetRotation, 0.05f)
        );
        seq.Join(
            itemTransform.DOScaleY(targetScale.y * 1.2f, 0.18f)
            .SetEase(Ease.OutQuad)
        );
        seq.Append(
            itemTransform.DOScale(targetScale, 0.12f)
            .SetEase(Ease.OutBack)
            .OnComplete(() => ServiceLocator.Get<SoundManager>().Play(SFX.PopItemPrateleira))
        );

        activeTweens++;
        isAnimating = true;

        seq.OnComplete(() =>
        {
            activeTweens--;

            if (activeTweens <= 0)
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
        shelfItem.Setup(this, groupIndex, spaceIndex);

        return true;
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
    if (!ServiceLocator.Get<ItemRaycastController>().isWithBox) return;

    ItemBox box = ServiceLocator.Get<ItemRaycastController>().LastBox();

    if(mySegment==Items.None && box.IsEmpty()) return;
    if(box.AllowedFurniture != myType && !box.AllowedFurniture.Equals(FurnitureType.None)){ print("Furniture Errada"); return;}
    if(box.isAnimating) return;

    if (box.IsEmpty())
    {
         isAnimating = true;
         TakeItem(box);

         OnLookAtWithRestriction();
         return;
    }

         Items type = box.GetBoxType();
        bool placedAnyItemFromBox = false;
        box.transform.root.DOPunchScale(-Vector3.right * .03f, .3f, 2);
        while (true)
        {
            Transform item = box.TakeItemByType(type);
            if (item == null)
             {  
                box.SetBoxType(Items.None);
                break;
                }

            Item itemComponent = item.GetComponent<Item>();

            if (!PlaceSingleItem(item,itemComponent.GetItemType()))
            {
                box.AddItem(item, type, this);

                break;
            }

            placedAnyItemFromBox = true;
        }

        if (placedAnyItemFromBox)
        {
            TutorialManager tutorialManager = ServiceLocator.Get<TutorialManager>();
            if (tutorialManager != null)
                tutorialManager.NotifyGameEvent("HasPutFood");
        }

        OnLookAtWithRestriction();

visualDelay = 0;

}
    public override bool OnLookAt()
    {
        isLooking = true;
        if(isAnimating)
       {
       // outlineMeshRenderer.material = transparentMaterial;

        ChangeMaterialColor(ServiceLocator.Get<FurnitureManager>().TransparentSegment, true);
        return false;
    }
        if (!ServiceLocator.Get<ItemRaycastController>().isWithBox) return false;
         ItemBox box = ServiceLocator.Get<ItemRaycastController>().LastBox();
          if(box.AllowedFurniture != myType && !box.AllowedFurniture.Equals(FurnitureType.None))
        {
        ChangeMaterialColor(ServiceLocator.Get<FurnitureManager>().RedSegment);
        PlayOutlineOnSound();
        return false;

        }
         if(box.IsEmpty() && mySegment == Items.None) return false;
       // if (mySegment != Items.None && mySegment != box.GetBoxType() && !box.IsEmpty()) return;
        if(box.GetBoxType() != mySegment && box.GetBoxType() != Items.None && mySegment != Items.None) return false;

        if(IsFull() && box.GetBoxType() != Items.None) return false;

        if (box.IsEmpty())
        {
          //  outlineMeshRenderer.material = redMaterial;

          ChangeMaterialColor(ServiceLocator.Get<FurnitureManager>().RedSegment);
        }
        else
        {
            // outlineMeshRenderer.material = greenMaterial;
            ChangeMaterialColor(ServiceLocator.Get<FurnitureManager>().GreenSegment);
        }
        PlayOutlineOnSound();
        return true;
    }
    public override void OnLookAway()
    {

      isLooking = false;
       //outlineMeshRenderer.material = transparentMaterial;
if(outlineMeshRenderer != null)
        ChangeMaterialColor(ServiceLocator.Get<FurnitureManager>().TransparentSegment, true);

        if (_outlineSoundOn)
        {
            _outlineSoundOn = false;
            ServiceLocator.Get<SoundManager>().Play(SFX.PararDeVerSegmento);
        }
    }

    bool _outlineSoundOn;
    void PlayOutlineOnSound()
    {
        if (_outlineSoundOn) return;
        _outlineSoundOn = true;
        ServiceLocator.Get<SoundManager>().Play(SFX.VerSegmentoInteragivel);
    }
    public void OnLookAtWithRestriction(){if(isLooking) ServiceLocator.Get<ItemRaycastController>().ReLook(this);}
    void ChangeMaterialColor(Color to,bool isTransparent = false)
    {
        HandleOutlineWidht(isTransparent);
         materialColorTween?.Kill();
          //  materialColorTween = outlineMeshRenderer.material.DOColor(to, .25f).SetEase(Ease.OutBack);
           Material mat = outlineMeshRenderer.material 
        ;

mat.EnableKeyword("_EMISSION");

if(to == ServiceLocator.Get<FurnitureManager>().GreenSegment)
        {
      



    DOTween.To(
    () => mat.GetColor("_EmissionColor"),
    x => mat.SetColor("_EmissionColor", x),
    ServiceLocator.Get<FurnitureManager>().GreenSegment,
    0.25f
).SetEase(Ease.OutBack);
HandleOutlineColor(  ServiceLocator.Get<FurnitureManager>().GreenOutline);
}else if(to == ServiceLocator.Get<FurnitureManager>().RedSegment)
        {
            
DOTween.To(
    () => mat.GetColor("_EmissionColor"),
    x => mat.SetColor("_EmissionColor", x),
   ServiceLocator.Get<FurnitureManager>().RedSegment,
    0.25f).SetEase(Ease.OutBack);
HandleOutlineColor(  ServiceLocator.Get<FurnitureManager>().RedOutline);

        }

    }
    void HandleOutlineWidht(bool isTransparent)
    {
      
        outlineWidhtTween?.Kill();
        if(isTransparent)
        {
             isOutlineTransiting = true;
          outlineWidhtTween = DOTween.To(
    () => outline.OutlineWidth,
    x => outline.OutlineWidth = x,
    0f,
    0.25f
).OnComplete(() => { outline.enabled = false; isOutlineTransiting = false; });
           
            DOTween.To(
    () => outlineMeshRenderer.material.GetColor("_EmissionColor"),
    x => outlineMeshRenderer.material.SetColor("_EmissionColor", x),
    new Color(0f, 0f, 0f),
    0.25f);


        }else
        {   
            outline.enabled = true; 
                   isOutlineTransiting = true;
            outlineWidhtTween = DOTween.To(
    () => outline.OutlineWidth,
    x => outline.OutlineWidth = x,
    10f,
    0.25f
).OnComplete(() => { isOutlineTransiting = false; });
        }


    }
     bool isOutlineTransiting;
     void HandleOutlineColor(Color to)
    {
        Color targetColor = to;

        outline.OutlineColor = targetColor;
    }
}