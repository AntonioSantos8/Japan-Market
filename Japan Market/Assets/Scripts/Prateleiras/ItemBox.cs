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

    public FurnitureType AllowedFurniture => allowedFurniture;
    [SerializeField] private Image itemIconImage;
    public Transform GetItemsParent() => itemsParent;

    private List<Transform> _spaces = new List<Transform>();
    private ItemGridSettings _gridSettings;
    private bool _isInitialized;

    void Start()
    {
        if (!_isInitialized)
        {
            if (boxType != Items.None)
            {
                InitializeBox(boxType);
            }
            else
            {
                UpdateVisual();
            }
        }
    }

    void EnsureItemsParent()
    {
        if (itemsParent == null)
            itemsParent = transform;
    }

    public void InitializeBox(Items type)
    {
        if (type == Items.None)
        {
            boxType = Items.None;
            allowedFurniture = FurnitureType.None;
            _spaces.Clear();
            _gridSettings = null;
            UpdateVisual();
            _isInitialized = true;
            return;
        }

        boxType = type;
        var itemManager = ServiceLocator.Get<ItemManager>();
        var data = itemManager != null ? itemManager.GetItemData(type) : null;

        if (data != null)
        {
            _gridSettings = data.boxGrid;
            if (data.allowedFurniture != FurnitureType.None)
                allowedFurniture = data.allowedFurniture;

            int capacity = _gridSettings != null ? _gridSettings.TotalCapacity : 0;
            _spaces = new List<Transform>(new Transform[capacity]);

            EnsureItemsParent();

            if (data.itemPrefab != null && capacity > 0)
            {
                for (int i = 0; i < capacity; i++)
                {
                    Vector3 localPos = _gridSettings.GetLocalPosition(i);
                    Quaternion localRot = _gridSettings.GetLocalRotation();
                    Vector3 localScale = _gridSettings.itemScale;

                    GameObject itemObj = Instantiate(data.itemPrefab, itemsParent);
                    itemObj.transform.localPosition = localPos;
                    itemObj.transform.localRotation = localRot;
                    itemObj.transform.localScale = localScale;

                    if (itemObj.TryGetComponent(out Rigidbody rb))
                        rb.isKinematic = true;

                    _spaces[i] = itemObj.transform;
                }
            }
        }
        else
        {
            _gridSettings = new ItemGridSettings();
            _spaces = new List<Transform>();
        }

        UpdateVisual();
        _isInitialized = true;
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
        Sprite icon = itemManager != null ? itemManager.GetItemIcon(boxType) : null;

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
        if (boxType == Items.None) return true;
        for (int i = 0; i < _spaces.Count; i++)
        {
            if (_spaces[i] != null) return false;
        }
        return true;
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
        if (!CanReceive(type)) return false;

        if (GetBoxType() == Items.None || _gridSettings == null)
        {
            boxType = type;
            var itemManager = ServiceLocator.Get<ItemManager>();
            var data = itemManager != null ? itemManager.GetItemData(type) : null;
            _gridSettings = data != null ? data.boxGrid : new ItemGridSettings();
            int capacity = _gridSettings != null ? _gridSettings.TotalCapacity : 0;
            _spaces = new List<Transform>(new Transform[capacity]);
            UpdateVisual();
        }

        int index = -1;
        for (int i = 0; i < _spaces.Count; i++)
        {
            if (_spaces[i] == null)
            {
                index = i;
                break;
            }
        }

        if (index == -1) return false;

        EnsureItemsParent();

        Vector3 end = _gridSettings.GetLocalPosition(index);
        Quaternion targetRotation = _gridSettings.GetLocalRotation();
        Vector3 targetScale = _gridSettings.itemScale;

        _spaces[index] = item;
        item.SetParent(itemsParent);

        ServiceLocator.Get<SoundManager>().Play(SFX.WooshTransicaoItem);

        Sequence seq = DOTween.Sequence();
        seq.SetDelay(visualDelay + Random.Range(0f, 0.015f));

        Vector3 start = item.localPosition;
        float distance = Vector3.Distance(start, end);
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

        seq.Append(
            item.DOLocalPath(path, 0.27f, PathType.CatmullRom)
            .SetEase(Ease.InOutCubic)
        );

        seq.Join(
            item.DORotate(
                new Vector3(
                    Random.Range(-15f, 15f),
                    Random.Range(-25f, 25f),
                    Random.Range(-10f, 10f)
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
            item.DOLocalRotateQuaternion(targetRotation, 0.08f)
        );

        seq.Append(
            item.DOScale(targetScale * 1.08f, 0.06f)
        );

        seq.Append(
            item.DOScale(targetScale, 0.12f)
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
            if (activeTweens <= 0)
            {
                isAnimating = false;
                if (segment != null)
                {
                    segment.IsAnimating = false;
                    segment.OnLookAtWithRestriction();
                }
            }
        });

        visualDelay += delayBetweenItems;

        if (item.TryGetComponent(out Rigidbody rb))
            rb.isKinematic = true;

        UpdateBoxType(type);
        if (segment != null)
            allowedFurniture = segment.FurnitureType;

        return true;
    }

    public void SetBoxType(Items type)
    {
        boxType = type;
        if (type == Items.None)
        {
            allowedFurniture = FurnitureType.None;
            _spaces.Clear();
            _gridSettings = null;
            UpdateVisual();
        }
    }
    
    public Transform TakeItemByType(Items type)
    {
        visualDelay = 0;
        if (boxType != type) return null;

        for (int i = _spaces.Count - 1; i >= 0; i--)
        {
            Transform item = _spaces[i];
            if (item == null) continue;

            _spaces[i] = null;

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
        UpdateVisual();
        return null;
    }
}