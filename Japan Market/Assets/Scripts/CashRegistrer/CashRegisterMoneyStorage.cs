using UnityEngine;
using DG.Tweening;
public class CashRegisterMoneyStorage : InteractableBase
{
    public bool IsOpen { get; private set; }
    private Tween _move;
    [SerializeField] Vector3 openLocalPosition, closedLocalPosition;
    [SerializeField] float openTime, closeTime;



    public override void Awake()
    {
        base.Awake();
        SetCanInteract(false);
    }

    // The register controls the drawer; player clicks cannot toggle it.
    public override void Interact() { }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        _move?.Kill();
        _move = transform.DOLocalMove(openLocalPosition, openTime).SetEase(Ease.OutBack);
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        _move?.Kill();
        _move = transform.DOLocalMove(closedLocalPosition, closeTime).SetEase(Ease.InBack);
    }

    private void OnDestroy()
    {
        _move?.Kill();
    }

    
    
}
