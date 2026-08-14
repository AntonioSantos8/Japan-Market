using UnityEngine;
using DG.Tweening;
public class MoneyInstance : InteractableBase
{
    [SerializeField] float moveDuration = 0.3f;
    [SerializeField] int jumpPower = 1;
    Vector3 originalPosition;
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
        transform.DOJump(originalPosition, moveDuration, jumpPower, moveDuration).SetEase(Ease.InOutSine).OnComplete(() =>
        {
            Destroy(gameObject);
        });
    }
}
