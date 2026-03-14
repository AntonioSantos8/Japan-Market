using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
public class ItemBox : MonoBehaviour
{
    [SerializeField] Items boxType;
    public SegmentTypeGroup[] groups;
    [SerializeField] Transform itemsParent;

    public Transform GetItemsParent() => itemsParent;

    void Start()
    {
         for (int i = 0; i < groups.Length; i++)
            groups[i].InitWithType(boxType);
    }

    public bool IsEmpty()
    {
        if(boxType == Items.None)
        {
            return true;


        }else return false;


        for (int g = 0; g < groups.Length; g++)
            for (int i = 0; i < groups[g].spaces.Count; i++)
                if (groups[g].spaces[i] != null)
                    return false;

        return true;
    }
float visualDelay;
[SerializeField] float delayBetweenItems = 0.08f;

public bool isAnimating;
int activeTweens;	
    public Items GetBoxType()
    {
        if (IsEmpty()) return Items.None;
        return boxType;
    }

    public void UpdateBoxType(Items newType)
    {
        if (boxType == Items.None)
            boxType = newType;
    }

    public bool CanReceive(Items type)
    {
        if (GetBoxType() == Items.None) return true;
        return boxType == type;
    }

 public bool AddItem(Transform item, Items type, Segment segment)
{
    for (int g = 0; g < groups.Length; g++)
    {
        if (groups[g].type != type) continue;

        int index = groups[g].GetNullSpace();
        if (index == -1) return false;

        Transform target = groups[g].allItems[index];

        groups[g].spaces[index] = item;

        item.SetParent(itemsParent);

    Sequence seq = DOTween.Sequence();

seq.SetDelay(visualDelay + Random.Range(0f,0.015f));

Vector3 start = item.localPosition;
Vector3 end = target.localPosition;

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
    item.DOLocalRotateQuaternion(target.localRotation, 0.08f)
);

seq.Append(
    item.DOScale(target.localScale * 1.08f, 0.06f)
);

seq.Append(
    item.DOScale(target.localScale, 0.12f)
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
		segment.isAnimating = false;
segment.OnLookAtWithRestriction();
}
        });

        visualDelay += delayBetweenItems;

        if (item.TryGetComponent(out Rigidbody rb))
            rb.isKinematic = true;

        UpdateBoxType(type);
        return true;
    }

    return false;
}
public void SetBoxType(Items type)
    {
        
        boxType = type;

    }
    public Transform TakeItemByType(Items type)
    {
visualDelay = 0;
        for (int g = 0; g < groups.Length; g++)
        {
            if (groups[g].type != type) continue;

            for (int i = groups[g].spaces.Count - 1; i >= 0; i--)
            {
                Transform item = groups[g].spaces[i];
                if (item == null) continue;

                groups[g].spaces[i] = null;

                if (IsEmpty())
                    boxType = Items.None;


                return item;
            }
        }
        boxType = Items.None;
        return null;
    }
}