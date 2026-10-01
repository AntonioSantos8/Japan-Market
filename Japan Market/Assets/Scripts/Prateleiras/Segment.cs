using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using JapanMarket.Gameplay;
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
    [SerializeField, Min(0.05f)] float placementInterval = 0.3f;
    [SerializeField] Material greenMaterial, redMaterial, transparentMaterial;
    [SerializeField] Shelf shelf;
    [SerializeField] FurnitureType myType;
    public FurnitureType FurnitureType => myType;
    public float PlacementInterval => Mathf.Max(0.05f, placementInterval);

    public SegmentTypeGroup[] Groups 
    { 
        get 
        { 
            InitializeGroups(); 
            return groups; 
        } 
        set => groups = value; 
    }
    [SerializeField] MeshRenderer outlineMeshRenderer;

    int activeTweens;
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
            if (itemData != null && (itemData.allowedFurniture == myType ||
                itemData.allowedFurniture == FurnitureType.None || myType == FurnitureType.None))
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
        itemTransform.SetParent(itemsParent);
        groups[groupIndex].spaces[spaceIndex] = itemTransform;

        ServiceLocator.Get<SoundManager>().Play(SFX.WooshTransicaoItem);

        Vector3 localPos = settings.GetLocalPosition(spaceIndex);
        Vector3 end = itemsParent.TransformPoint(localPos);
        Quaternion targetRotation = itemsParent.rotation * settings.GetLocalRotation();
        Vector3 targetScale = settings.itemScale;

        Vector3 start = itemTransform.position;
        float arcHeight = Mathf.Min(Vector3.Distance(start, end) * 0.14f, 0.28f);
        const float moveDuration = 0.4f;

        Sequence seq = DOTween.Sequence();
        // Um arco contínuo evita a pausa no waypoint inicial e o salto curto
        // que havia entre a trajetória e o encaixe final.
        seq.Append(DOVirtual.Float(0f, 1f, moveDuration, progress =>
        {
            itemTransform.position = Vector3.Lerp(start, end, progress)
                + Vector3.up * (4f * arcHeight * progress * (1f - progress));
        }).SetEase(Ease.OutSine));
        seq.Join(itemTransform.DORotateQuaternion(targetRotation, moveDuration)
            .SetEase(Ease.OutSine));
        seq.Join(itemTransform.DOScale(targetScale, moveDuration)
            .SetEase(Ease.OutSine));

        activeTweens++;
        isAnimating = true;

        seq.OnComplete(() =>
        {
            itemTransform.SetPositionAndRotation(end, targetRotation);
            itemTransform.localScale = targetScale;
            ServiceLocator.Get<SoundManager>().Play(SFX.PopItemPrateleira);
            ShelfPlacementEffect.Play(itemTransform.gameObject);

            activeTweens--;

            if (activeTweens <= 0)
            {
                isAnimating = false;
                OnLookAtWithRestriction();
            }
        });

        ShelfItem shelfItem = itemTransform.GetComponent<ShelfItem>();
        if (shelfItem == null)
            shelfItem = itemTransform.gameObject.AddComponent<ShelfItem>();

        shelf.RegisterSegment(type, this);
        shelfItem.Setup(this, groupIndex, spaceIndex);

        return true;
    }

    public bool CanPlaceFromBox(ItemBox box)
    {
        return box != null && !box.IsEmpty() && !box.isAnimating
            && (box.AllowedFurniture == myType || box.AllowedFurniture == FurnitureType.None)
            && (mySegment == Items.None || mySegment == box.GetBoxType())
            && !IsFull();
    }

    public bool CanTakeIntoBox(ItemBox box)
    {
        return box != null && mySegment != Items.None && !isAnimating
            && (box.AllowedFurniture == myType || box.AllowedFurniture == FurnitureType.None)
            && box.HasSpaceFor(mySegment);
    }

    public bool TakeOneItem(ItemBox box)
    {
        if (!CanTakeIntoBox(box)) return false;

        for (int g = 0; g < groups.Length; g++)
        {
            if (groups[g].type != mySegment) continue;

            for (int i = groups[g].spaces.Count - 1; i >= 0; i--)
            {
                Transform item = groups[g].spaces[i];
                if (item == null) continue;
                if (!box.AddItem(item, groups[g].type, this)) return false;

                RemoveItem(g, i);
                if (IsEmpty()) shelf.RemoveSegment(this);
                OnLookAtWithRestriction();
                return true;
            }
        }

        return false;
    }
 public override void Interact()
{
    ItemRaycastController controller = ServiceLocator.Get<ItemRaycastController>();
    if (controller == null || !controller.isWithBox) return;

    ItemBox box = controller.LastBox();
    if (box == null) return;

    if (!CanPlaceFromBox(box)) return;

        Items type = box.GetBoxType();
        Transform item = box.TakeItemByType(type);
        if (item == null) return;

        Item itemComponent = item.GetComponent<Item>();
        if (!PlaceSingleItem(item, itemComponent.GetItemType()))
        {
            box.AddItem(item, type, this);
        }
        else
        {
            TutorialManager tutorialManager = ServiceLocator.Get<TutorialManager>();
            if (tutorialManager != null)
                tutorialManager.NotifyGameEvent("HasPutFood");
        }

        OnLookAtWithRestriction();
}
    public override bool OnLookAt()
    {
        isLooking = true;
        ItemRaycastController controller = ServiceLocator.Get<ItemRaycastController>();
        ItemBox box = controller != null ? controller.LastBox() : null;
        if (controller == null || !controller.isWithBox || box == null) return false;
        bool canPlace = CanPlaceFromBox(box);
        bool canTake = CanTakeIntoBox(box);
        if (!canPlace && !canTake) return false;

        ChangeMaterialColor(canPlace
            ? ServiceLocator.Get<FurnitureManager>().GreenSegment
            : ServiceLocator.Get<FurnitureManager>().RedSegment);
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
