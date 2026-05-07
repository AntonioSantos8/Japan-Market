using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;
using UnityEngine.Events;
using System;

[RequireComponent(typeof(RectTransform))]

[Serializable]
public class ShakeScaleParams
{

    public float duration;
    public Vector3 strenght = Vector3.one;
    public int vibrato = 10;
    public float randomness = 90;

}
[Serializable]
public class ShakeRotationParams
{

    public float duration;
    public Vector3 strenght = Vector3.one;
    public int vibrato = 10;
    public float randomness = 90;

}
public class UIButtonAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
{
   
    [SerializeField] float duration = 0.25f;
    [SerializeField] Ease ease = Ease.OutBack;

    
    [SerializeField] bool animateScale = true;
    [SerializeField] Vector3 hoverScale = new Vector3(1.1f, 1.1f, 1f);

    [SerializeField] bool shakeScaleOnHover;
    [SerializeField] ShakeScaleParams shakeScaleParams;
    [SerializeField] Transform parentToShake;
    
    [SerializeField] bool animateRotation = false;
    [SerializeField] Vector3 hoverRotation = new Vector3(0f, 0f, 10f);

    [SerializeField] bool shakeRotationOnHover;
    [SerializeField] ShakeRotationParams shakeRotationParams;
    [SerializeField] Transform parentToRotate;

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

    float lerpSpeed;
    [SerializeField] float normalSpeed = 2, hoverSpeed = 2, clickSpeed = 2;
 
    Vector3 targetScale;

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
    void Update()
    {
        
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale + baseScale, lerpSpeed * Time.deltaTime);

    }
    public void OnPointerEnter(PointerEventData eventData)
    {
        targetScale = hoverScale;
        lerpSpeed = hoverSpeed;
        if (shakeScaleOnHover) 
        {
            parentToShake.DOShakeScale(shakeScaleParams.duration, shakeScaleParams.strenght, shakeScaleParams.vibrato, shakeScaleParams.randomness);
        }
        if (shakeRotationOnHover)
        {
            parentToShake.DOShakeRotation(shakeRotationParams.duration, shakeRotationParams.strenght, shakeRotationParams.vibrato, shakeRotationParams.randomness);
        }
        PlaySound(hoverClip);
        onSelect?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        targetScale = Vector3.zero;
         lerpSpeed = normalSpeed;
      
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

    void PlaySound(AudioClip clip)
    {
        if (!useAudio || clip == null) return;
        audioSource.PlayOneShot(clip);
    }

    public void OnSelect(BaseEventData eventData)
    {
        targetScale = hoverScale;
         lerpSpeed = hoverSpeed;
        if (shakeScaleOnHover)
        {
            parentToShake.DOShakeScale(shakeScaleParams.duration, shakeScaleParams.strenght, shakeScaleParams.vibrato, shakeScaleParams.randomness);
        }
        if (shakeRotationOnHover)
        {
            parentToShake.DOShakeRotation(shakeRotationParams.duration, shakeRotationParams.strenght, shakeRotationParams.vibrato, shakeRotationParams.randomness);
        }
        PlaySound(hoverClip);
        onSelect?.Invoke();
    }

    public void OnDeselect(BaseEventData eventData)
    {
                targetScale = Vector3.zero;
        lerpSpeed = normalSpeed;
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