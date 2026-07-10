using UnityEngine;
using TMPro;
using DG.Tweening;

/// <summary>
/// Handles card payment flow. CashRegister calls Open/Close; this class
/// manages the number input, UI updates and payment confirmation.
/// </summary>
public class PaymentCard : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────
    [SerializeField] private GameObject      imagepayment;
    [SerializeField] private TextMeshProUGUI valueText;
    [SerializeField] private CashRegister    cashRegister;

    // ── State ─────────────────────────────────────────────────────────────────
    public bool IsOpen { get; private set; }

    private float   totalPrice;
    private string  currentValue = "";
    private Vector3 _imageOriginalScale;
    private Canvas  _canvas;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _imageOriginalScale = imagepayment.transform.localScale;
        _canvas = imagepayment.GetComponentInChildren<Canvas>(true);
        if (_canvas == null) _canvas = imagepayment.GetComponentInParent<Canvas>();
        imagepayment.SetActive(false);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Open(float price)
    {
        if (IsOpen) return;

        IsOpen     = true;
        totalPrice = price;

        ServiceLocator.Get<TutorialManager>()?.NotifyGameEvent("ChoosedPaymentType");

        currentValue = "";
        imagepayment.SetActive(true);
        imagepayment.transform.localScale = _imageOriginalScale;

        if (_canvas != null) { _canvas.enabled = false; _canvas.enabled = true; }
        Canvas.ForceUpdateCanvases();

        cashRegister.PaymentTextCash("Card Checkout");
        RefreshUI();
    }

    public void Close()
    {
        IsOpen = false;
        imagepayment.SetActive(false);
    }

    // ── Number Input (called by UI buttons) ───────────────────────────────────

    public void AddNumber(string number)
    {
        if (currentValue.Length >= 6) return;
        currentValue += number;
        RefreshUI();
        valueText.transform.DOPunchScale(Vector3.one * 0.1f, 0.21f, 2, 0.12f);
    }

    public void Delete()
    {
        if (currentValue.Length == 0) return;
        currentValue = currentValue.Remove(currentValue.Length - 1);
        RefreshUI();
    }

    public void AddPoint() { /* Yen has no decimal subunit — button kept for UI completeness */ }

    public void Confirm()
    {
        if (!float.TryParse(currentValue, out float typed)) return;

        bool correct = Mathf.Abs(typed - totalPrice) < 0.5f;
        if (correct) OnPaymentSuccess();
        else         OnPaymentError();
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void RefreshUI()
    {
        valueText.text = currentValue.Length == 0 ? "¥ 0" : "¥ " + currentValue;
    }

    private void OnPaymentSuccess()
    {
        ServiceLocator.Get<MarketManager>().Earn_Money(totalPrice);

        DOTween.Sequence()
            .Append(valueText.DOColor(Color.green, 0.2f))
            .AppendInterval(0.2f)
            .Append(valueText.DOColor(Color.white, 0.2f))
            .Append(imagepayment.transform.DOScale(0f, 0.26f).SetEase(Ease.InOutSine))
            .OnComplete(() =>
            {
                IsOpen = false;
                imagepayment.SetActive(false);
                cashRegister.FinalizeTransaction();
            });
    }

    private void OnPaymentError()
    {
        currentValue = "";
        RefreshUI();
        cashRegister.ApplyPenalty();

        DOTween.Sequence()
            .Append(valueText.DOColor(Color.red, 0.2f))
            .Join(valueText.transform.DOShakePosition(0.3f, 5.3f, 20))
            .Append(valueText.DOColor(Color.white, 0.2f));
    }
}
