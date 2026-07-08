using UnityEngine;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;
public class PaymentMoney : InteractableBase
{
    private const string ChoosedPaymentTypeEventId = "ChoosedPaymentType";

    [SerializeField] GameObject imagePayment;
    [SerializeField] TextMeshPro receivedText;
    [SerializeField] TextMeshPro changeText;
    [SerializeField] TextMeshPro givingText;
    [SerializeField] CashRegister cashRegister;
    [SerializeField] List<float> possiblePayments = new List<float>();
    [SerializeField] float clickRadius = 0.2f;
    private List<float> moneyStack = new List<float>();
    private Camera mainCamera;
    private bool isPaymentOpen;
    float totalPrice;
    float customerPaid;
    float giving = 0f;
    //Vector3 originalScale;
    void Start()
    {
        mainCamera = Camera.main;
        imagePayment.SetActive(false);
        originalScale = imagePayment.transform.localScale;
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
    public void OpenPayment()
    {
        isPaymentOpen = true;

        TutorialManager tutorialManager = ServiceLocator.Get<TutorialManager>();
        if (tutorialManager != null)
            tutorialManager.NotifyGameEvent(ChoosedPaymentTypeEventId);

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

      customerPaid = Mathf.Round(customerPaid); // iene não tem subunidade decimal
    }
    public void AddMoney(float value)
    {
        moneyStack.Add(value);
        giving += value;

        UptadeValue();

        givingText.transform.DOKill();
        givingText.transform.DOPunchScale(Vector3.one * 0.011f, 0.21f, 2, 0.12f);
           
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

        receivedText.text = "Received: ¥" + Mathf.RoundToInt(customerPaid);
        changeText.text = "Change: ¥" + Mathf.RoundToInt(change);
        givingText.text = "Giving: ¥" + Mathf.RoundToInt(giving);
    }
    public void Confirm()
    {
        float correctChange = customerPaid - totalPrice;

        // Tolerância de meio iene (em vez de 0.01) porque os valores agora são
        // sempre inteiros — 0.01 era resquício de comparação em centavos.
        if (Mathf.Abs(giving - correctChange) < 0.5f)
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
        seq.Append(givingText.DOColor(Color.white, 0.2f));
        seq.Append(imagePayment.transform.DOScale(0f, 0.25f).SetEase(Ease.InOutSine));

        seq.OnComplete(() =>
        {
            isPaymentOpen = false;

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
        isPaymentOpen = false;
        imagePayment.SetActive(false);
    }
}