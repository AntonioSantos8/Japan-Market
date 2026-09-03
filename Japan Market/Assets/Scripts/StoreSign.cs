using DG.Tweening;
using UnityEngine;
public class StoreSign : InteractableBase
{
    [SerializeField] float rotationY = 90f;
    [SerializeField] float duration = 1f;
    [SerializeField] float moveBack = 0.32f;
    [SerializeField] Ease ease = Ease.OutBack;
    [SerializeField] Transform placaTransform;
    TutorialManager _tutorialManager;
    bool isRotating = false;
    bool isOpen = false;
    Vector3 originalPos;
    float originalYRotation;
    Warnings warnings;
    MarketManager marketManager;
    NpcManager npcManager;
    void Start()
    {
        originalPos = placaTransform.localPosition;
        originalYRotation = placaTransform.eulerAngles.y;
        warnings = ServiceLocator.Get<Warnings>();
        marketManager = ServiceLocator.Get<MarketManager>();
        npcManager = ServiceLocator.Get<NpcManager>();
        _tutorialManager = ServiceLocator.Get<TutorialManager>();
    }
    public override void Interact()
    {
        Rotate();
    }
    void Rotate()
    {
        if (isRotating) return;

        if (warnings.IsWarningActive) return;

        if (isOpen)
        {
            warnings.ShowWarning("Store is Closed!", false);
        }
        else
        {
            warnings.ShowWarning("Store is Open!", true);
            if(_tutorialManager)
            {
                _tutorialManager.NotifyGameEvent("StoreOpened");
            }

        }
        isRotating = true;
        float targetRotation = isOpen
            ? originalYRotation
            : originalYRotation + rotationY;

        Sequence seq = DOTween.Sequence();
        seq.Append(placaTransform.DOLocalMoveZ(originalPos.z - moveBack, duration * 0.3f).SetEase(Ease.OutSine));
        seq.Append(placaTransform.DOLocalRotate(new Vector3(0, targetRotation, 0), duration).SetEase(ease));
        seq.Join(placaTransform.DOLocalMoveZ(originalPos.z, duration).SetEase(Ease.OutBack));
        seq.OnComplete(() =>
        {
            isRotating = false;
            isOpen = !isOpen;
            marketManager.Open = isOpen;
            ServiceLocator.Get<SoundManager>().Play(SFX.LojaAbertaFechada);
            if (isOpen)
                npcManager.StartSpawning();
            else
                npcManager.StopSpawning();
        });
    }
}