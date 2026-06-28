using UnityEngine;
using TMPro;
using DG.Tweening;
public class PaymentCard : InteractableBase
{
    [SerializeField] GameObject imagepayment;
    [SerializeField] TextMeshProUGUI valueText;
    [SerializeField] CashRegister cashRegister;
    [SerializeField] float clickRadius = 0.2f;
    private Camera mainCamera;
    private bool isPaymentOpen;
    string currentValue = "";
    float totalPrice;
  //  Vector3 originalScale;
    void Start()
    {
        mainCamera = Camera.main;
        imagepayment.SetActive(false);
        originalScale = imagepayment.transform.localScale;
    }
    public override void Interact()
    {
        var controller = ServiceLocator.Get<ItemRaycastController>();
        controller.PickItem(rb, true);
    }

    // Substitui OnMouseDown (raycast interno da Unity, que para no primeiro
    // collider que encontra) por um SphereCastAll, igual ao usado pro clique
    // nos itens: assim o trigger do balcão na frente não bloqueia o clique.
    void Update()
    {
        if (!isPaymentOpen && Input.GetMouseButtonDown(0) && IsClickedByMouse())
            OpenPayment();
    }

    private bool IsClickedByMouse()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.SphereCastAll(ray, clickRadius);

        foreach (var hit in hits)
        {
            if (hit.collider.gameObject == gameObject)
                return true;
        }

        return false;
    }
    void OpenPayment()
    {
        isPaymentOpen = true;

        imagepayment.SetActive(true);
        imagepayment.transform.localScale = originalScale;
        cashRegister.PaymentTextCash("Card Checkout");
        totalPrice = cashRegister.GetTotalPrice();

        currentValue = "";
        valueText.text = "¥ 0";
    }
    public void AddNumber(string number)
    {
        if (currentValue.Length >= 6) return;

        currentValue += number;
        UpdateText();

        valueText.transform.DOPunchScale(Vector3.one * 0.1f, 0.21f, 2, 0.12f);
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
        ServiceLocator.Get<MarketManager>().Earn_Money(totalPrice);
        seq.Append(valueText.DOColor(Color.darkGreen, 0.2f));
        seq.AppendInterval(0.2f);
        seq.Append(valueText.DOColor(Color.white, 0.2f));
        seq.Append(imagepayment.transform.DOScale(0f, 0.26f).SetEase(Ease.InOutSine));

        seq.OnComplete(() =>
        {
            isPaymentOpen = false;

            imagepayment.SetActive(false);

            cashRegister.FinalizeTransaction();
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

        cashRegister.ApplyPenalty();
    }
    public void ClosePaymentCredi()
    {
        isPaymentOpen = false;
        imagepayment.SetActive(false);
    }
}