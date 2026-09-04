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
        ResolveServices();
    }

    /// <summary>
    /// Resolve os serviços de forma preguiçosa. O ServiceLocator devolve null
    /// silenciosamente quando o serviço ainda não foi registrado, e a ordem entre
    /// Start() de componentes diferentes é indefinida (e muda entre Editor e build).
    /// Por isso tentamos de novo a cada interação em vez de confiar no cache do Start.
    /// </summary>
    void ResolveServices()
    {
        if (warnings == null) warnings = ServiceLocator.Get<Warnings>();
        if (marketManager == null) marketManager = ServiceLocator.Get<MarketManager>();
        if (npcManager == null) npcManager = ServiceLocator.Get<NpcManager>();
        if (_tutorialManager == null) _tutorialManager = ServiceLocator.Get<TutorialManager>();
    }

    public override void Interact()
    {
        Rotate();
    }
    void Rotate()
    {
        if (isRotating) return;

        ResolveServices();

        if (warnings != null && warnings.IsWarningActive) return;

        if (isOpen)
        {
            if (warnings != null) warnings.ShowWarning("Store is Closed!", false);
        }
        else
        {
            if (warnings != null) warnings.ShowWarning("Store is Open!", true);
            if (_tutorialManager)
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
        seq.SetLink(gameObject);
        seq.OnComplete(() =>
        {
            isRotating = false;
            isOpen = !isOpen;

            // Nada aqui pode lançar excecao: se lançar, a callback aborta e o
            // StartSpawning() la de baixo nunca corre (era exatamente o bug da build).
            ResolveServices();

            if (marketManager != null)
                marketManager.Open = isOpen;
            else
                Debug.LogError("[StoreSign] MarketManager nao registrado no ServiceLocator.");

            if (npcManager == null)
            {
                Debug.LogError("[StoreSign] NpcManager nao registrado no ServiceLocator - NPCs nao vao spawnar.");
                return;
            }

            if (isOpen)
                npcManager.StartSpawning();
            else
                npcManager.StopSpawning();
        });
    }
}
