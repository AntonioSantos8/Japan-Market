using UnityEngine;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// Handles cash payment flow. CashRegister calls Open/Close; this class
/// manages the money stack, UI updates and payment confirmation.
/// </summary>
public class PaymentMoney : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────
    [SerializeField] private GameObject   imagePayment;
    [SerializeField] private TextMeshPro  receivedText;
    [SerializeField] private TextMeshPro  changeText;
    [SerializeField] private TextMeshPro  givingText;
    [SerializeField] private CashRegister cashRegister;
    [SerializeField] private List<float>  possiblePayments = new List<float>();

    // ── State ─────────────────────────────────────────────────────────────────
    public bool IsOpen { get; private set; }

    private List<float> moneyStack   = new List<float>();
    private float       totalPrice;
    private float       customerPaid;
    private float       giving;
    private Vector3     _imageOriginalScale;
    private Canvas      _canvas;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _imageOriginalScale = imagePayment.transform.localScale;
        // Cache the Canvas so we can force-refresh it on Open().
        // Unity sometimes skips the first render of a Canvas reactivated via SetActive.
        _canvas = imagePayment.GetComponentInChildren<Canvas>(true);
        if (_canvas == null) _canvas = imagePayment.GetComponentInParent<Canvas>();
        imagePayment.SetActive(false);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Open(float price)
    {
        if (IsOpen) return;

        IsOpen     = true;
        totalPrice = price;

        ServiceLocator.Get<TutorialManager>()?.NotifyGameEvent("ChoosedPaymentType");

        customerPaid = CalculateCustomerPayment();
        moneyStack.Clear();
        giving = 0f;

        // O pagamento em dinheiro agora é físico: a nota/moeda entra na mesa,
        // não há mais UI de abrir um painel para adicionar dinheiro.
        if (imagePayment != null)
            imagePayment.SetActive(false);

        if (receivedText != null) receivedText.gameObject.SetActive(true);
        if (changeText != null) changeText.gameObject.SetActive(true);
        if (givingText != null) givingText.gameObject.SetActive(true);

        cashRegister.PaymentTextCash("Cash Checkout");
        RefreshUI();
    }

    public void Close()
    {
        IsOpen = false;
        if (imagePayment != null)
            imagePayment.SetActive(false);
    }

    
    [ContextMenu("Add ¥1")]
    public void AddOne() => AddMoney(1f);
    Tweener addOneTween;
    private bool CanUseCashPayment()
    {
        if (cashRegister == null)
        {
            Debug.LogWarning("[PaymentMoney] Bloqueado: CashRegister não foi atribuído.");
            return false;
        }

        bool canUse = cashRegister.GetCurrentPaymentType() == PaymentType.Cash;
        if (!canUse)
            Debug.LogWarning($"[PaymentMoney] Bloqueado: cliente paga com {cashRegister.GetCurrentPaymentType()}, não com dinheiro.");

        return canUse;
    }

    public void AddMoney(float value)
    {
        if (!CanUseCashPayment()) return;
        if (value <= 0f) return;

        moneyStack.Add(value);
        giving += value;
        RefreshUI();
        if(addOneTween != null && !addOneTween.IsPlaying())
       addOneTween= givingText.transform.DOPunchScale(Vector3.one * 0.011f, 0.21f, 2, 0.12f);

    }

    public void RemoveMoney(float value)
    {
        if (!CanUseCashPayment()) return;
        if (value <= 0f || !moneyStack.Remove(value)) return;

        giving = Mathf.Max(0f, giving - value);
        RefreshUI();
    }

    public void Undo()
    {
        if (!CanUseCashPayment()) return;
        if (moneyStack.Count == 0) return;
        float last = moneyStack[moneyStack.Count - 1];
        moneyStack.RemoveAt(moneyStack.Count - 1);
        giving -= last;
        RefreshUI();
    }

    public void ClearAll()
    {
        if (!CanUseCashPayment()) return;
        moneyStack.Clear();
        giving = 0f;
        RefreshUI();
    }

    public void Confirm()
    {
        if (!CanUseCashPayment()) return;
        float correctChange = customerPaid - totalPrice;
        bool correct = Mathf.Abs(giving - correctChange) < 0.5f;
        if (correct) OnPaymentSuccess();
        else         OnPaymentError();
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private float CalculateCustomerPayment()
    {
        if (possiblePayments.Count == 0) return totalPrice;

        var valid = possiblePayments.FindAll(v => v >= totalPrice);
        float result = valid.Count > 0
            ? valid[Random.Range(0, valid.Count)]
            : possiblePayments[possiblePayments.Count - 1];

        return Mathf.Round(result);
    }

    private void RefreshUI()
    {
        float change      = Mathf.Max(0f, customerPaid - totalPrice);
        receivedText.text = "Received: ¥" + Mathf.RoundToInt(customerPaid);
        changeText.text   = "Change: ¥"   + Mathf.RoundToInt(change);
        givingText.text   = "Giving: ¥"   + Mathf.RoundToInt(giving);
    }

    private void OnPaymentSuccess()
    {
        ServiceLocator.Get<MarketManager>().Earn_Money(totalPrice);

        DOTween.Sequence()
            .Append(givingText.DOColor(Color.green, 0.2f))
            .AppendInterval(0.2f)
            .Append(givingText.DOColor(Color.white, 0.2f))
            .OnComplete(() =>
            {
                IsOpen = false;
                if (imagePayment != null) imagePayment.SetActive(false);
                if (receivedText != null) receivedText.gameObject.SetActive(false);
                if (changeText != null) changeText.gameObject.SetActive(false);
                if (givingText != null) givingText.gameObject.SetActive(false);
                cashRegister.FinalizeTransaction();
            });
    }

    private void OnPaymentError()
    {
        cashRegister.ApplyPenalty();
        cashRegister.PaymentTextCash("Troco incorreto!");

        DOTween.Sequence()
            .Append(givingText.DOColor(Color.red, 0.2f))
            .Join(givingText.transform.DOShakePosition(0.3f, 0.02f, 15))
            .Append(givingText.DOColor(Color.white, 0.2f))
            .OnComplete(ClearAll);
    }
}
