using UnityEngine;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;
public class PaymentMoney : MonoBehaviour
{
    [SerializeField] GameObject imagePayment;
    [SerializeField] TextMeshPro receivedText;
    [SerializeField] TextMeshPro changeText;
    [SerializeField] TextMeshPro givingText;
    [SerializeField] CashRegister cashRegister;
    [SerializeField] List<float> possiblePayments = new List<float>();
    private List<float> moneyStack = new List<float>();
    float totalPrice;
    float customerPaid; 
    float giving = 0f;
    Vector3 originalScale;
    void Start()
    {
        imagePayment.SetActive(false);
        originalScale = imagePayment.transform.localScale;
    }
    void OnMouseDown()
    {
        OpenPayment();
    }
    public void OpenPayment()
    {
        imagePayment.SetActive(true);
        imagePayment.transform.localScale = originalScale;
        cashRegister.PaymentTextCash("Cash Checkout");
        receivedText.gameObject.SetActive(true);
        changeText.gameObject.SetActive(true);
        givingText.gameObject.SetActive(true);

        totalPrice = cashRegister.GetTotalPrice();

        CustomerPayment();

        moneyStack.Clear();
        giving = 0f;

        UptadeValue();
    }
    void CustomerPayment()
    {
        if (possiblePayments.Count == 0)
        {
           customerPaid = totalPrice;
            return;
        }
        List<float> validPayments = new List<float>();

        foreach (var value in possiblePayments)
        {
            if (value >= totalPrice)
                validPayments.Add(value);
        }

        if (validPayments.Count == 0)
        {
            customerPaid = possiblePayments[possiblePayments.Count - 1];
        }
        else
        {
            customerPaid = validPayments[Random.Range(0, validPayments.Count)];
        }

      customerPaid = Mathf.Round(customerPaid * 100f) / 100f;
    }
    public void AddMoney(float value)
    {
        moneyStack.Add(value);
        giving += value;

        UptadeValue();

        givingText.transform.DOKill();

        givingText.transform
           .DOScale(1.01f, 0.1f)
            .OnComplete(() =>
            {
                givingText.transform.DOScale(1f, 0.1f);
            });
    }
    public void Undo()
    {
        if (moneyStack.Count == 0) return;

        float last = moneyStack[moneyStack.Count - 1];
        giving -= last;

        moneyStack.RemoveAt(moneyStack.Count - 1);

        UptadeValue();
    }
    public void ClearAll()
    {
        moneyStack.Clear();
        giving = 0f;

        UptadeValue();
    }
    void UptadeValue()
    {
        float change = Mathf.Max(0, customerPaid - totalPrice);

        receivedText.text = "Received: ¥" + customerPaid.ToString("F2");
        changeText.text = "Change: ¥" + change.ToString("F2");
        givingText.text = "Giving: ¥" + giving.ToString("F2");
    }
    public void Confirm()
    {
        float correctChange = customerPaid - totalPrice;

        if (Mathf.Abs(giving - correctChange) < 0.01f)
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
        ServiceLocator.Get<MarketManager>().Earn_Money(totalPrice);
        seq.Append(givingText.DOColor(Color.green, 0.2f));
        seq.AppendInterval(0.2f);
        seq.Append(imagePayment.transform.DOScale(0f, 0.4f).SetEase(Ease.InOutSine));

        seq.OnComplete(() =>
        {
            imagePayment.SetActive(false);
            receivedText.gameObject.SetActive(false);
            changeText.gameObject.SetActive(false);
            givingText.gameObject.SetActive(false);

            cashRegister.FinalizeTransaction();
        });
    }

    void PaymentError()
    {
        Sequence seq = DOTween.Sequence();

        seq.Append(givingText.DOColor(Color.red, 0.2f));
        seq.Join(givingText.transform.DOShakePosition(0.3f, 0.02f, 15));
        seq.Append(givingText.DOColor(Color.white, 0.2f));

        seq.OnComplete(() =>
        {
            ClearAll();
        });

        cashRegister.ApplyPenalty();
    }
    public void ClosePaymentMoney()
    {
        imagePayment.SetActive(false);
    }
}