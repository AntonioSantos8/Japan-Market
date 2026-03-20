using DG.Tweening;
using UnityEngine;
public class StoreSign : MonoBehaviour
{
    [SerializeField] private float rotationAmount = 90f; 
    [SerializeField] private float duration = 0.5f; 
    [SerializeField] private Ease ease = Ease.OutBack;
    private bool isRotating = false;

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

        if (Physics.Raycast(ray, out hit))
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

        transform
            .DORotate(new Vector3(0, transform.eulerAngles.y + rotationAmount, 0), duration)
            .SetEase(ease)
            .OnComplete(() => isRotating = false);
    }
}
