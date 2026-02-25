using UnityEngine;
using DG.Tweening;

public class AutomaticDoorTrigger : MonoBehaviour
{
    [SerializeField] private Transform doorleft;
    [SerializeField] private Transform doorRigth;
    [SerializeField] private Vector3 leftClosedPos;
    [SerializeField] private Vector3 leftOpenPos;
    [SerializeField] private Vector3 rightClosedPos;
    [SerializeField] private Vector3 rightOpenPos;
    [SerializeField] private float speed = 1f;
    [SerializeField] private float doorCloseTime = 2f;
    [SerializeField] private Ease easeType = Ease.OutCubic;
    private Tween leftTween;
    private Tween rightTween;
    private bool playerInside;
    private float closeTimer;

    private void Update()
    {
        if (!playerInside)
        {
            closeTimer += Time.deltaTime;

            if (closeTimer >= doorCloseTime)
            {
                CloseDoors();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = true;
            closeTimer = 0f;
            OpenDoors();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = false;
            closeTimer = 0f;
        }
    }

    private void OpenDoors()
    {
        leftTween?.Kill();
        rightTween?.Kill();

        leftTween = doorleft.DOMove(leftOpenPos, speed).SetEase(easeType);
        rightTween = doorRigth.DOMove(rightOpenPos, speed).SetEase(easeType);
    }

    private void CloseDoors()
    {
        leftTween?.Kill();
        rightTween?.Kill();

        leftTween = doorleft.DOMove(leftClosedPos, speed).SetEase(Ease.InCubic);
        rightTween = doorRigth.DOMove(rightClosedPos, speed).SetEase(Ease.InCubic);
    }
}