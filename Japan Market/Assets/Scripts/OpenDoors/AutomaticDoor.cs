using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class AutomaticDoor : MonoBehaviour
{
    [SerializeField] Transform doorleft;
    [SerializeField] Transform doorRigth;
    [SerializeField] Vector3 leftClosedPos;
    [SerializeField] Vector3 leftOpenPos;
    [SerializeField] Vector3 rightClosedPos;
    [SerializeField] Vector3 rightOpenPos;
    [SerializeField] bool useWorldOffsets;
    [SerializeField] Vector3 leftOpenWorldOffset;
    [SerializeField] Vector3 rightOpenWorldOffset;
    [SerializeField] float speed = 1f;
    [SerializeField] float doorCloseTime = 2f;
    [SerializeField] Ease easeType = Ease.OutCubic;
    Tween leftTween;
    Tween rightTween;
    readonly HashSet<Collider> occupants = new();
    bool isOpen;
    float closeTimer;

    TutorialManager _tutorialManager;

    void Awake()
    {
        if (doorleft == null)
        {
            Debug.LogError("[AutomaticDoor] A folha principal da porta precisa estar configurada.", this);
            enabled = false;
            return;
        }

        if (useWorldOffsets)
        {
            leftClosedPos = doorleft.position;
            leftOpenPos = leftClosedPos + leftOpenWorldOffset;

            if (doorRigth != null)
            {
                rightClosedPos = doorRigth.position;
                rightOpenPos = rightClosedPos + rightOpenWorldOffset;
            }
            return;
        }

     
        Vector3 leftTravel = leftOpenPos - leftClosedPos;
        Vector3 rightTravel = rightOpenPos - rightClosedPos;

        leftClosedPos = doorleft.localPosition;
        if (doorRigth != null) rightClosedPos = doorRigth.localPosition;

        if (leftTravel.sqrMagnitude < 0.01f || leftTravel.sqrMagnitude > 25f)
            leftTravel = Vector3.right * 1.25f;
        if (doorRigth != null && (rightTravel.sqrMagnitude < 0.01f || rightTravel.sqrMagnitude > 25f))
            rightTravel = Vector3.left * 1.25f;

        leftOpenPos = leftClosedPos + leftTravel;
        if (doorRigth != null) rightOpenPos = rightClosedPos + rightTravel;
    }

    public void ConfigureWorldDoor(Transform primaryLeaf, Transform secondaryLeaf,
                                   Vector3 primaryOpenOffset, Vector3 secondaryOpenOffset)
    {
        doorleft = primaryLeaf;
        doorRigth = secondaryLeaf;
        useWorldOffsets = true;
        leftOpenWorldOffset = primaryOpenOffset;
        rightOpenWorldOffset = secondaryOpenOffset;
    }

    void Start() => ResolveTutorial();

    void ResolveTutorial()
    {
        if (_tutorialManager == null)
            _tutorialManager = ServiceLocator.Get<TutorialManager>();
    }

    private void Update()
    {
        occupants.RemoveWhere(collider => collider == null);

        if (isOpen && occupants.Count == 0)
        {
            closeTimer += Time.deltaTime;

            if (closeTimer >= doorCloseTime)
                CloseDoors();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.CompareTag("NPC"))
        {
            occupants.Add(other);
            closeTimer = 0f;
            OpenDoors(other.CompareTag("Player"));
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.CompareTag("NPC"))
        {
            occupants.Remove(other);
            closeTimer = 0f;
        }
    }

    private void OpenDoors(bool playerEntered)
    {
        if (playerEntered)
        {
            ResolveTutorial();
            _tutorialManager?.NotifyGameEvent("EnteredStore");
        }

        // Só toca o som na transição fechada -> aberta, não a cada vez que
        // alguém entra no trigger com a porta já aberta.
        if (!isOpen)
        {
            isOpen = true;

            // `?.` e não chamada direta. O ServiceLocator devolve null quando o
            // serviço não está registrado, e esta linha roda ANTES dos tweens:
            // sem SoundManager, a NullReferenceException acontecia aqui e a
            // porta simplesmente nunca se movia — sem nenhuma pista de que o
            // problema era o som.
            ServiceLocator.Get<SoundManager>()?.Play(SFX.PortaAutomaticaAbrir);
        }

        leftTween?.Kill();
        rightTween?.Kill();

        leftTween = useWorldOffsets
            ? doorleft.DOMove(leftOpenPos, speed).SetEase(easeType)
            : doorleft.DOLocalMove(leftOpenPos, speed).SetEase(easeType);
        if (doorRigth != null)
            rightTween = useWorldOffsets
                ? doorRigth.DOMove(rightOpenPos, speed).SetEase(easeType)
                : doorRigth.DOLocalMove(rightOpenPos, speed).SetEase(easeType);
    }

    private void CloseDoors()
    {
        if (!isOpen) return;

        isOpen = false;

        leftTween?.Kill();
        rightTween?.Kill();

        leftTween = useWorldOffsets
            ? doorleft.DOMove(leftClosedPos, speed).SetEase(Ease.InCubic)
            : doorleft.DOLocalMove(leftClosedPos, speed).SetEase(Ease.InCubic);
        if (doorRigth != null)
            rightTween = useWorldOffsets
                ? doorRigth.DOMove(rightClosedPos, speed).SetEase(Ease.InCubic)
                : doorRigth.DOLocalMove(rightClosedPos, speed).SetEase(Ease.InCubic);
    }

    private void OnDisable()
    {
        leftTween?.Kill();
        rightTween?.Kill();
        occupants.Clear();
    }
}
