using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.UI;
public class ItemBox : MonoBehaviour
{
    [SerializeField] Items boxType;
    [SerializeField] FurnitureType allowedFurniture;

    [SerializeField] Transform itemsParent;
    [SerializeField] float delayBetweenItems = 0.08f;

    float visualDelay;
    public bool isAnimating;
    int activeTweens;

    Vector3[] slotLocalPositions = new Vector3[0];
    List<Transform> slotOccupants = new List<Transform>();

    public FurnitureType AllowedFurniture => allowedFurniture;
    [SerializeField] private Image itemIconImage;
    public Transform GetItemsParent() => itemsParent;

    void Start()
    {
        if (boxType != Items.None && slotOccupants.Count == 0)
        {
            AllIThingsData data = ServiceLocator.Get<ItemManager>().GetItemData(boxType);
            if (data != null) Populate(data);
        }

        UpdateVisual();
    }

    public void Populate(AllIThingsData data)
    {
        boxType = data.itemType;

        slotLocalPositions = data.boxGrid.GenerateLocalPositions();
        slotOccupants = new List<Transform>(new Transform[slotLocalPositions.Length]);

        for (int i = 0; i < slotLocalPositions.Length; i++)
        {
            Transform spawned = Instantiate(data.itemPrefab, itemsParent).transform;
            spawned.localPosition = slotLocalPositions[i];
            spawned.localRotation = Quaternion.Euler(data.boxGrid.itemRotationEuler);
            spawned.localScale = data.boxGrid.itemScale;

            if (spawned.TryGetComponent(out Rigidbody rb))
                rb.isKinematic = true;

            slotOccupants[i] = spawned;
        }

        UpdateVisual();
    }

    int GetNullSlot()
    {
        for (int i = 0; i < slotOccupants.Count; i++)
            if (slotOccupants[i] == null) return i;
        return -1;
    }

    public void UpdateVisual()
    {
        if (itemIconImage == null) return;

        if (boxType == Items.None)
        {
            itemIconImage.enabled = false;
            return;
        }

        var itemManager = ServiceLocator.Get<ItemManager>();
        Sprite icon = itemManager.GetItemIcon(boxType);

        if (icon != null)
        {
            itemIconImage.sprite = icon;
            itemIconImage.enabled = true;
        }
        else
        {
            itemIconImage.enabled = false;
        }
    }
    public bool IsEmpty()
    {
        if(boxType == Items.None)
        {
            return true;


        }else return false;


        // for (int g = 0; g < groups.Length; g++)
        //     for (int i = 0; i < groups[g].spaces.Count; i++)
        //         if (groups[g].spaces[i] != null)
        //             return false;

        // return true;
    }
    public Items GetBoxType()
    {
        if (IsEmpty()) return Items.None;
        return boxType;
    }

    public void UpdateBoxType(Items newType)
    {
        if (boxType != newType)
        {
            boxType = newType;
            UpdateVisual();
        }
    }
    public bool CanReceive(Items type)
    {
        if (GetBoxType() == Items.None) return true;
        return boxType == type;
    }

 public bool AddItem(Transform item, Items type, Segment segment)
{
    if (boxType == Items.None)
    {
        AllIThingsData newData = ServiceLocator.Get<ItemManager>().GetItemData(type);
        slotLocalPositions = newData.boxGrid.GenerateLocalPositions();
        slotOccupants = new List<Transform>(new Transform[slotLocalPositions.Length]);
    }

    if (boxType != Items.None && boxType != type) return false;

    int index = GetNullSlot();
    if (index == -1) return false;

    Vector3 target = slotLocalPositions[index];

    slotOccupants[index] = item;

        item.SetParent(itemsParent);

    Sequence seq = DOTween.Sequence();

seq.SetDelay(visualDelay + Random.Range(0f,0.015f));

Vector3 start = item.localPosition;
Vector3 end = target;

float distance = Vector3.Distance(start,end);
float height = distance * 0.21f;

Vector3 mid = (start + end) * 0.5f;
mid += Vector3.up * height;

Vector3[] path = new Vector3[]
{
    start,
    mid,
    end
};

Vector3 originalScale = item.localScale;

AllIThingsData data = ServiceLocator.Get<ItemManager>().GetItemData(type);
Quaternion targetLocalRotation = Quaternion.Euler(data.boxGrid.itemRotationEuler);
Vector3 targetLocalScale = data.boxGrid.itemScale;

seq.Append(
    item.DOLocalPath(path, 0.27f, PathType.CatmullRom)
    .SetEase(Ease.InOutCubic)
);

seq.Join(
    item.DORotate(
        new Vector3(
            Random.Range(-15f,15f),
            Random.Range(-25f,25f),
            Random.Range(-10f,10f)
        ),
        0.35f
    ).SetEase(Ease.OutSine)
);

seq.Join(
    item.DOScale(originalScale * 0.9f, 0.2f)
);

seq.Append(
    item.DOLocalMove(end, 0.08f)
    .SetEase(Ease.InQuad)
);

seq.Join(
    item.DOLocalRotateQuaternion(targetLocalRotation, 0.08f)
);

seq.Append(
    item.DOScale(targetLocalScale * 1.08f, 0.06f)
);

seq.Append(
    item.DOScale(targetLocalScale, 0.12f)
    .SetEase(Ease.OutBack)
);

seq.Append(
    item.DOPunchPosition(Vector3.down * 0.03f, 0.12f, 6, 0.7f)
);


        activeTweens++;
        isAnimating = true;

        seq.OnComplete(() =>
        {
            activeTweens--;
            if (activeTweens <= 0){
                isAnimating = false;
		segment.IsAnimating = false;
segment.OnLookAtWithRestriction();
}
        });

        visualDelay += delayBetweenItems;

        if (item.TryGetComponent(out Rigidbody rb))
            rb.isKinematic = true;

        UpdateBoxType(type);
        allowedFurniture = segment.FurnitureType;
        return true;
    }
public void SetBoxType(Items type)
    {

        boxType = type;

    }

    public Transform TakeItemByType(Items type)

    {
visualDelay = 0;

        if (boxType != type) return null;

        for (int i = slotOccupants.Count - 1; i >= 0; i--)
        {
            Transform item = slotOccupants[i];
            if (item == null) continue;

            slotOccupants[i] = null;

            if (IsEmpty())
            {
                allowedFurniture = FurnitureType.None;
                boxType = Items.None;
                UpdateVisual();
            }

            return item;
        }

        allowedFurniture = FurnitureType.None;
        boxType = Items.None;
        return null;
    }
}
