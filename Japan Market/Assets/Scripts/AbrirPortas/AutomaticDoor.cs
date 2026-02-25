using UnityEngine;
using DG.Tweening;
public class AutomaticDoor : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Vector3 closePos;
    [SerializeField] private Vector3 openPos;
    [SerializeField] private float Speed = 1f;
    [SerializeField] private float distance = 3f;
    [SerializeField] private float doorCloseTime = 2f;
    [SerializeField] private Ease easeType = Ease.OutCubic;
    private bool isOpen;
    private Tween currentTween;
    private float closeTimer;
    
    private void Update()
    {
        float dist = Vector3.Distance(transform.position, player.position);

        if (dist <= distance)
        {
            closeTimer = 0f;

            if (!isOpen)
                OpenDoor();
        }
        else
        {
            if (isOpen)
            {
                closeTimer += Time.deltaTime;

                if (closeTimer >= doorCloseTime)
                    CloseDoor();
            }
        }
    }

    private void OpenDoor()
    {
        isOpen = true;

        currentTween?.Kill();

        currentTween = transform.DOMoveX(openPos.x, Speed)
       .SetEase(easeType);
    }

    private void CloseDoor()
    {
        isOpen = false;

        currentTween?.Kill();

        currentTween = transform.DOMove(closePos, Speed)
            .SetEase(Ease.InCubic);
    }
}