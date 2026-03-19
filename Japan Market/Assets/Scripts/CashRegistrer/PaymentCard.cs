using UnityEngine;
using TMPro;
using DG.Tweening;
public class PaymentCard : MonoBehaviour
{
    [SerializeField] GameObject imagepayment;
    [SerializeField] TextMeshProUGUI valueText;
    [SerializeField] CashRegister cashRegister;
    string currentValue = "";
    float totalPrice;
    void Start()
    {
        imagepayment.SetActive(false);
    }
    void OnMouseDown()
    {
        OpenPayment();
    }
    void OpenPayment()
    {
        imagepayment.SetActive(true);

        totalPrice = cashRegister.GetTotalPrice();

        currentValue = "";
        valueText.text = "¥ 0";
    }
    public void AddNumber(string number)
    {
        if (currentValue.Length >= 6) return;

        currentValue += number;
        UpdateText();

        valueText.transform.DOPunchScale(Vector3.one * 0.1f, 0.2f, 10, 1);
    }
    public void AddPoint()
    {
        if (!currentValue.Contains("."))
        {
            if (currentValue == "")
                currentValue = "0.";

            else
                currentValue += ".";

            UpdateText();
        }
    }
    public void Delete()
    {
        if (currentValue.Length == 0) return;

        currentValue = currentValue.Remove(currentValue.Length - 1);
        UpdateText();
    }
    void UpdateText()
    {
        if (currentValue == "")
            valueText.text = "¥ 0";
        else
            valueText.text = "¥ " + currentValue;
    }
    public void Confirm()
    {
        float typedValue;

        if (!float.TryParse(currentValue, out typedValue))
            return;

        if (Mathf.Abs(typedValue - totalPrice) < 0.01f)
        {
            PaymentSuccess();
        }
        else
        {
            PaymentError();
        }
    }
    void PaymentSuccess()
    {
        Sequence seq = DOTween.Sequence();

        seq.Append(valueText.DOColor(Color.darkGreen, 0.2f));
        seq.AppendInterval(0.2f);
        seq.Append(imagepayment.transform.DOScale(0f,0.6f).SetEase(Ease.InOutBack));

        seq.OnComplete(() =>
        {
            imagepayment.SetActive(false);
            cashRegister.FinishPayment();
        });
    }
    void PaymentError()
    {
        currentValue = "";
        UpdateText();

        Sequence seq = DOTween.Sequence();

        seq.Append(valueText.DOColor(Color.red, 0.2f));
        seq.Join(valueText.transform.DOShakePosition(0.3f, 5.3f, 20));

        seq.Append(valueText.DOColor(Color.white, 0.2f));
    }
    public void ClosePaymentCredi()
    {
        imagepayment.SetActive(false);
    }
}