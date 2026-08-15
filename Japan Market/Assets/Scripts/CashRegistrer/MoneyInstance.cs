using UnityEngine;
using DG.Tweening;
public class MoneyInstance : InteractableBase
{
    [SerializeField] float moveDuration = 0.3f;
    [SerializeField] int jumpPower = 1;
    Vector3 originalPosition;
    CashRegister cashRegister;
    float value;

    public void Setup(CashRegister register, float moneyValue)
    {
        cashRegister = register;
        value = moneyValue;
    }

    void Start()
    {
        canInteract = false;
        originalPosition = transform.position;
    }
    public void GoToPosition(Vector3 position)
    {
        transform.DOJump(position, moveDuration, jumpPower, moveDuration).SetEase(Ease.InOutSine).OnComplete(() =>
        {
            canInteract = true;
        });
    }

    public override void Interact()
    {
        if(!canInteract) return;
        canInteract = false;
        cashRegister?.RemoveMoney(value);
        transform.DOJump(originalPosition, moveDuration, jumpPower, moveDuration).SetEase(Ease.InOutSine).OnComplete(() =>
        {
            Destroy(gameObject);
        });
    }
}
