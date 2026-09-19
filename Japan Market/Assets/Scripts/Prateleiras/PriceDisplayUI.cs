using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.Events;
public class PriceDisplayUI : MonoBehaviour
{
     [SerializeField] TMP_Text _currentPrice;
    [SerializeField] TMP_Text _marketPrice;
    [SerializeField] Button applyButton;
    [SerializeField] Transform normalPos, returnPos;
    [SerializeField] float transitionTime;
    [SerializeField] Ease ease;
    [SerializeField] UnityEvent onFinishReturn;
    Tween transitionTween;
    Tween scaleTween;
    Vector3 inicialScale;
    bool initialized;

    public void ShowDisplay(float currentPrice, float marketPrice)
    {
        // This window is allowed to start inactive in the prefab/scene. A method
        // can still be invoked through its stored reference while inactive, but
        // Unity will not draw it until the GameObject is explicitly re-enabled.
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        InitializeIfNeeded();

        transitionTween?.Kill();
        scaleTween?.Kill();
        transitionTween = transform.DOMove(normalPos.position, transitionTime).SetEase(ease);
        transform.localScale = new Vector3(0, inicialScale.y, inicialScale.z);
        scaleTween = transform.DOScale(inicialScale, transitionTime).SetEase(ease);
        ServiceLocator.Get<ItemRaycastController>()?.SetGeneralCanInteract(false);
        if (_currentPrice != null) _currentPrice.text = "¥" + Mathf.RoundToInt(currentPrice);
        if (_marketPrice != null) _marketPrice.text = "¥" + Mathf.RoundToInt(marketPrice);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        PlayerLook look = ServiceLocator.Get<PlayerLook>();
        if (look != null) look.CanLook = false;
        ServiceLocator.Get<PlayerMotor>()?.SetCanMove(false);

    }
    private void Awake()
    {
        InitializeIfNeeded();

        // The scene used to rely on somebody unchecking this GameObject by hand.
        // Keep it active and place it in its real hidden state instead, so the
        // first tutorial request can always bring it back.
        if (returnPos != null) transform.position = returnPos.position;
        transform.localScale = new Vector3(0, inicialScale.y, inicialScale.z);
    }

    void InitializeIfNeeded()
    {
        if (initialized) return;
        inicialScale = transform.localScale;
        if (Mathf.Approximately(inicialScale.x, 0f)) inicialScale.x = 1f;
        initialized = true;
    }
    public void CloseDisplay() 
    {
        InitializeIfNeeded();
        transitionTween?.Kill();
        scaleTween?.Kill();
        if (returnPos != null)
            transitionTween = transform.DOMove(returnPos.position, transitionTime)
                .OnComplete(() => { onFinishReturn?.Invoke(); });
        else
            onFinishReturn?.Invoke();

        scaleTween = transform.DOScale(new Vector3(0, inicialScale.y, inicialScale.z),
                                       transitionTime + .1f).SetEase(ease);



    }
    public void LockMouse()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        PlayerLook look = ServiceLocator.Get<PlayerLook>();
        if (look != null) look.CanLook = true;
        ServiceLocator.Get<PlayerMotor>()?.SetCanMove(true);
    }
   
    public void SetCanInteractTrue(){ ServiceLocator.Get<ItemRaycastController>()?.SetGeneralCanInteract(true); }

    private void OnDisable()
    {
        transitionTween?.Kill();
        scaleTween?.Kill();
    }
   
}
