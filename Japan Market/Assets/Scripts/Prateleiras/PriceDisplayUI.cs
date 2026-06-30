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
    Vector3 inicialScale;
    public void ShowDisplay(float currentPrice, float marketPrice)
    {
        transitionTween?.Kill();
        transitionTween = transform.DOMove(normalPos.position, transitionTime).SetEase(ease);
        transform.localScale = new Vector3(0, inicialScale.y, inicialScale.z);
          transform.DOScale(inicialScale, transitionTime ).SetEase(ease);
        ServiceLocator.Get<ItemRaycastController>().SetGeneralCanInteract(false);
        _currentPrice.text = "¥" + Mathf.RoundToInt(currentPrice);
        _marketPrice.text = "¥" + Mathf.RoundToInt(marketPrice);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        ServiceLocator.Get<PlayerLook>().CanLook = false;
        ServiceLocator.Get<PlayerMotor>().SetCanMove(false);

    }
    private void Awake()
    {
        inicialScale = transform.localScale;
    }
    public void CloseDisplay() 
    {
        transitionTween?.Kill();
        transitionTween=  transform.DOMove(returnPos.position, transitionTime).OnComplete(() => { onFinishReturn?.Invoke(); });
         
        transform.DOScale(new Vector3(0, inicialScale.y, inicialScale.z), transitionTime + .1f).SetEase(ease);



    }
    public void LockMouse()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        ServiceLocator.Get<PlayerLook>().CanLook = true; 
        ServiceLocator.Get<PlayerMotor>().SetCanMove(true);
    }
   
    public void SetCanInteractTrue(){ ServiceLocator.Get<ItemRaycastController>().SetGeneralCanInteract(true); }
   
}
