using UnityEngine;
using TMPro;
using System.Text;
using System.Globalization;
public class NumbersDisplay : MonoBehaviour
{
     [SerializeField] TMP_Text _displayText;

    private StringBuilder currentValue = new StringBuilder();
    private bool hasDot = false;

    public void AddNumber(int number)
    {
        if (number < 0 || number > 9) return;

        currentValue.Append(number);
        UpdateDisplay();
    }
public void ResetInputs()
    {
       currentValue.Clear(); 



    }  


  public void AddDot()
    {
        if (hasDot) return;

        if (currentValue.Length == 0)
            currentValue.Append("0");

        currentValue.Append(".");
        hasDot = true;
        UpdateDisplay();
    }

    public void RemoveLast()
    {
        if (currentValue.Length == 0) return;

        if (currentValue[currentValue.Length - 1] == '.'){
            hasDot = false;
        }

        currentValue.Remove(currentValue.Length - 1, 1);
        UpdateDisplay();
    }

   public float Apply(Items itemType)
{
    if (currentValue.Length == 0) return ServiceLocator.Get<GlobalPrices>().GetItemCurrentPrice(itemType);

    float value = float.Parse(currentValue.ToString(), CultureInfo.InvariantCulture);
    currentValue.Clear();
    hasDot = false;
    UpdateDisplay();

    return value;
}
   private void UpdateDisplay()
{
    if (currentValue.Length == 0)
    {  _displayText.text = "0.00";
        return;
    }

    if (float.TryParse(currentValue.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out float value))
       {_displayText.text = value.ToString("F2", CultureInfo.InvariantCulture);
       }else
        {_displayText.text = currentValue.ToString();}
}
}
