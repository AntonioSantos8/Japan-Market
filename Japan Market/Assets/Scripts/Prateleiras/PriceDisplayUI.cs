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
        
        _currentPrice.text = currentPrice.ToString();
        _marketPrice.text = marketPrice.ToString();
        gameObject.SetActive(true);
 Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

    }
    public void LockMouse()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

    }
   
}
