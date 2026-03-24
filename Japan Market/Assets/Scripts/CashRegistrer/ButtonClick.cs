using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
public class ButtonClick : MonoBehaviour
{
    [SerializeField] float scaleButton = 0.85f;
    [SerializeField] float duration = 0.1f;
    [SerializeField] Ease ease = Ease.OutBack;
    Vector3 originalScale;
    Button button;

    void Start()
    {
        originalScale = transform.localScale;
        button = GetComponent<Button>();

        button.onClick.AddListener(ClickButton);
    }
    void ClickButton()
    {
      transform.DOKill();
     Sequence seq = DOTween.Sequence();

     seq.Append(transform.DOScale(originalScale * scaleButton, duration))
           .Append(transform.DOScale(originalScale * 1.1f, duration))
           .Append(transform.DOScale(originalScale, duration))
           .SetEase(ease);
    }
}