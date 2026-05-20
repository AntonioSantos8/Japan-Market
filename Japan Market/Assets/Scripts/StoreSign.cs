using DG.Tweening;
using Unity.Android.Gradle.Manifest;
using UnityEngine;
public class StoreSign : InteractableBase
{
    [SerializeField] float rotationY = 90f;
    [SerializeField] float duration = 0.5f;
    [SerializeField] float moveBack = 0.33f;
    [SerializeField] Ease ease = Ease.OutBack;
    bool isRotating = false;
    bool isOpen = false;
    Vector3 originalPos;
    float originalYRotation;
    void Start()
    {
        originalPos = transform.localPosition;
        originalYRotation = transform.eulerAngles.y;
    }
    public override void Interact()
    {
        Rotate();
    }
    void Rotate()
    {
        if (isRotating) return;

        if (ServiceLocator.Get<Warnings>().IsWarningActive) return;

        if (isOpen)
        {
            ServiceLocator.Get<Warnings>().ShowWarning("Store is Closed!", false);
        }
        else
        {
            ServiceLocator.Get<Warnings>().ShowWarning("Store is Open!", true);
        }
        isRotating = true;
        float targetRotation = isOpen
            ? originalYRotation
            : originalYRotation + rotationY;

        Sequence seq = DOTween.Sequence();
        seq.Append(transform.DOMoveZ(originalPos.z - moveBack, duration * 0.3f).SetEase(Ease.OutQuad));
        seq.Append(transform.DORotate(new Vector3(0, targetRotation, 0), duration).SetEase(ease));
        seq.Join(transform.DOMoveZ(originalPos.z, duration).SetEase(Ease.OutBack));
        seq.OnComplete(() =>
        {
            isRotating = false;
            isOpen = !isOpen;
            ServiceLocator.Get<MarketManager>().Open = isOpen;
            if (isOpen)
                ServiceLocator.Get<NpcManager>().StartSpawning();
            else
                ServiceLocator.Get<NpcManager>().StopSpawning();
        });
    }
}