using UnityEngine;
using DG.Tweening;
public class MoneyInstance : InteractableBase
{
    [SerializeField] float moveDuration = 0.3f;
    Vector3 originalPosition;
    void Start()
    {
        canInteract = false;
        originalPosition = transform.position;
    }
    public void GoToPosition(Vector3 position)
    {
        transform.DOMove(position, moveDuration).SetEase(Ease.InOutSine).OnComplete(() =>
        {
            canInteract = true;
        });
    }

    public override void Interact()
    {
        if(!canInteract) return;
        canInteract = false;
        transform.DOMove(originalPosition, moveDuration).SetEase(Ease.InOutSine).OnComplete(() =>
        {
            Destroy(gameObject);
        });
    }
}
