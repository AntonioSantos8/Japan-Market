using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;
using UnityEngine.Events;

[RequireComponent(typeof(RectTransform))]
public class UIButtonAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
{
   
    [SerializeField] float duration = 0.25f;
    [SerializeField] Ease ease = Ease.OutBack;

    
    [SerializeField] bool animateScale = true;
    [SerializeField] Vector3 hoverScale = new Vector3(1.1f, 1.1f, 1f);

    
    [SerializeField] bool animateRotation = false;
    [SerializeField] Vector3 hoverRotation = new Vector3(0f, 0f, 10f);

   
    [SerializeField] bool animatePosition = false;
    [SerializeField] Vector3 hoverPositionOffset = new Vector3(0f, 5f, 0f);

    
    [SerializeField] bool animateClick = true;
    [SerializeField] Vector3 clickScale = new Vector3(0.9f, 0.9f, 1f);
    [SerializeField] float clickDuration = 0.15f;
    [SerializeField] Ease clickEase = Ease.InOutSine;

   
    [SerializeField] bool useAudio = false;
    [SerializeField] AudioClip hoverClip;
    [SerializeField] AudioClip unhoverClip;
    [SerializeField] AudioClip clickClip;

    RectTransform rect;
    Vector3 baseScale;
    Quaternion baseRotation;
    Vector3 basePosition;
    Tween activeTween;
    AudioSource audioSource;

    public UnityEvent onSelect;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        baseScale = rect.localScale;
        baseRotation = rect.localRotation;
        basePosition = rect.localPosition;

        if (useAudio)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        AnimateTo(hoverScale + transform.localScale, hoverRotation, basePosition + hoverPositionOffset, duration, ease);
        PlaySound(hoverClip);
        onSelect?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        AnimateTo(baseScale, baseRotation.eulerAngles, basePosition, duration, ease);
        PlaySound(unhoverClip);
    }

    public void PointerExit()
    {
        AnimateTo(baseScale, baseRotation.eulerAngles, basePosition, duration, ease);
        PlaySound(unhoverClip);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!animateClick) return;

        Sequence seq = DOTween.Sequence();
        seq.Append(rect.DOScale(clickScale, clickDuration).SetEase(clickEase).SetUpdate(true));
        seq.Append(rect.DOScale(animateScale ? hoverScale : baseScale, clickDuration).SetEase(clickEase).SetUpdate(true));

        PlaySound(clickClip);
    }
    public void PlayClickSound()
    {
        PlaySound(clickClip);
    }

    void AnimateTo(Vector3 targetScale, Vector3 targetRotation, Vector3 targetPosition, float time, Ease easing)
    {
        activeTween?.Kill();
        Sequence seq = DOTween.Sequence();

        if (animateScale)
            seq.Join(rect.DOScale(targetScale, time).SetEase(easing).SetUpdate(true));

        if (animateRotation)
            seq.Join(rect.DOLocalRotate(targetRotation, time).SetEase(easing).SetUpdate(true));

        if (animatePosition)
            seq.Join(rect.DOLocalMove(targetPosition, time).SetEase(easing).SetUpdate(true));

        activeTween = seq;
    }

    void PlaySound(AudioClip clip)
    {
        if (!useAudio || clip == null) return;
        audioSource.PlayOneShot(clip);
    }

    public void OnSelect(BaseEventData eventData)
    {
        AnimateTo(hoverScale + transform.localScale, hoverRotation, basePosition + hoverPositionOffset, duration, ease);
        PlaySound(hoverClip);
        onSelect?.Invoke();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        AnimateTo(baseScale, baseRotation.eulerAngles, basePosition, duration, ease);
        PlaySound(unhoverClip);
    }
    public void Desselect()
    {
        AnimateTo(baseScale, baseRotation.eulerAngles, basePosition, duration, ease);
        PlaySound(unhoverClip);
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (!animateClick) return;

        Sequence seq = DOTween.Sequence();
        seq.Append(rect.DOScale(clickScale, clickDuration).SetEase(clickEase).SetUpdate(true));
        seq.Append(rect.DOScale(animateScale ? hoverScale : baseScale, clickDuration).SetEase(clickEase).SetUpdate(true));

        PlaySound(clickClip);
    }
}   