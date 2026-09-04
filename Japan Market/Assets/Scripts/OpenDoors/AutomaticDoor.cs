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
    [SerializeField] float speed = 1f;
    [SerializeField] float doorCloseTime = 2f;
    [SerializeField] Ease easeType = Ease.OutCubic;
    Tween leftTween;
   Tween rightTween;
   bool playerInside;
   float closeTimer;

   TutorialManager _tutorialManager;

    void Start()
    {
        _tutorialManager = ServiceLocator.Get<TutorialManager>();
    }
    private void Update()
    {
        if (!playerInside)
        {
            closeTimer += Time.deltaTime;

            if (closeTimer <= doorCloseTime)
            {
                CloseDoors();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.CompareTag("NPC"))
        {
            playerInside = true;
            closeTimer = 0f;
            OpenDoors();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.CompareTag("NPC"))
        {
            playerInside = false;
            closeTimer = 0f;
        }
    }

    private void OpenDoors()
    {

        if(_tutorialManager)
        _tutorialManager.NotifyGameEvent("EnteredStore");
        ServiceLocator.Get<SoundManager>().Play(SFX.PortaAutomaticaAbrir);
        leftTween?.Kill();
        rightTween?.Kill();

        leftTween = doorleft.DOLocalMove(leftOpenPos, speed).SetEase(easeType);
        rightTween = doorRigth.DOLocalMove(rightOpenPos, speed).SetEase(easeType);
    }

    private void CloseDoors()
    {
        leftTween?.Kill();
        rightTween?.Kill();

        leftTween = doorleft.DOLocalMove(leftClosedPos, speed).SetEase(Ease.InCubic);
        rightTween = doorRigth.DOLocalMove(rightClosedPos, speed).SetEase(Ease.InCubic);
    }
}