using DG.Tweening;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
public class NumbersDisplay : MonoBehaviour
{
     [SerializeField] TMP_Text _displayText;
    [SerializeField] Transform normalPos, returnPos;
    [SerializeField] float transitionTime;
    [SerializeField] Ease ease;
    Tween transitionTween;
    Vector3 inicialScale;
    public void GoToReturnPos() { transform.localPosition = returnPos.localPosition ; }
    public void ShowNumbers()
    {
        transitionTween?.Kill();
        transitionTween = transform.DOLocalMove(normalPos.localPosition, transitionTime).SetEase(Ease.OutBack);
        transform.localScale = new Vector3(inicialScale.x, 0, inicialScale.z);
        transform.DOScale(inicialScale, transitionTime).SetEase(ease);
   
       

    }
   
    private void Awake()
    {
        inicialScale = transform.localScale;
    }

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
