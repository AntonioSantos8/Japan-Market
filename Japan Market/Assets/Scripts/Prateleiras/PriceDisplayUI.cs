using UnityEngine;
using TMPro;
using UnityEngine.UI;
public class PriceDisplayUI : MonoBehaviour
{
     [SerializeField] TMP_Text _currentPrice;
    [SerializeField] TMP_Text _marketPrice;
    [SerializeField] Button applyButton;
    public void ShowDisplay(float currentPrice, float marketPrice)
    {
         ServiceLocator.Get<ItemRaycastController>().SetCanInteract(false);
        _currentPrice.text = currentPrice.ToString();
        _marketPrice.text = marketPrice.ToString();
        gameObject.SetActive(true);
 Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        ServiceLocator.Get<PlayerLook>().CanLook = false;
         ServiceLocator.Get<PlayerMotor>().SetCanMove(false);

    }
    public void LockMouse()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        ServiceLocator.Get<PlayerLook>().CanLook = true; 
        ServiceLocator.Get<PlayerMotor>().SetCanMove(true);
    }
   
    public void SetCanInteractTrue(){ ServiceLocator.Get<ItemRaycastController>().SetCanInteract(true); }
   
}
