using DG.Tweening;
using UnityEngine;
public class StoreSign : MonoBehaviour
{
    [SerializeField] float rotationY = 90f;
    [SerializeField] float duration = 0.5f;
    [SerializeField] float moveBack = 0.33f;
    [SerializeField] float interactDistance = 5f;
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
    void Update()
    {
        if (Input.GetButtonDown("Fire1"))
        {
            TryRotate();
        }
    }
    void TryRotate()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactDistance))
        {
            if (hit.transform == transform && !isRotating)
            {
                Rotate();
            }
        }
    }
    void Rotate()
    {
        isRotating = true;

        float targetRotation = isOpen
            ? originalYRotation              
            : originalYRotation + rotationY; 

        Sequence seq = DOTween.Sequence();

        seq.Append(
            transform.DOLocalMoveZ(originalPos.z - moveBack, duration * 0.3f)
            .SetEase(Ease.OutQuad)
        );
        seq.Append(
            transform
                .DORotate(new Vector3(0, targetRotation, 0), duration)
                .SetEase(ease)
        );

        seq.Join(
            transform.DOLocalMoveZ(originalPos.z, duration)
            .SetEase(Ease.OutBack)
        );

        seq.OnComplete(() =>
        {
            isRotating = false;
            isOpen = !isOpen;
        });
    }
}