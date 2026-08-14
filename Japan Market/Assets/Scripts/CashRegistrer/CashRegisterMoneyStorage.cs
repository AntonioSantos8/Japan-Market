using UnityEngine;
using DG.Tweening;
public class CashRegisterMoneyStorage : InteractableBase
{
    bool isOpen = false;
    [SerializeField] Vector3 openLocalPosition, closedLocalPosition;
    [SerializeField] float openTime, closeTime;



    public override void Interact()
    {
        if (!isOpen)
        {
            isOpen = true;
            transform.DOLocalMove(openLocalPosition, openTime).SetEase(Ease.OutBack);
        }
        else
        {
            isOpen = false;
            transform.DOLocalMove(closedLocalPosition, closeTime).SetEase(Ease.InBack);
        }
    }

    
    
}
