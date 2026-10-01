using UnityEngine;
using TMPro;
using DG.Tweening;

/// <summary>
/// Handles card payment flow. CashRegister/CardMachine call Open/Close; this
/// class manages the number input, UI updates and payment confirmation.
/// </summary>
public class PaymentCard : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────
    [SerializeField] private TextMeshProUGUI valueText;
    [SerializeField] private CashRegister    cashRegister;

    // ── State ─────────────────────────────────────────────────────────────────
    public bool IsOpen { get; private set; }

    private float  totalPrice;
    private bool processingPayment;
    private Sequence _feedback;
    private string currentValue = "" ;

    // ── Public API ────────────────────────────────────────────────────────────

    public void Open(float price)
    {
        if (IsOpen || cashRegister == null || !cashRegister.CanUseCardMachine) return;

        IsOpen     = true;
        processingPayment = false;
        totalPrice = price;
        currentValue = "";

        ServiceLocator.Get<TutorialManager>()?.NotifyGameEvent("ChoosedPaymentType");
        cashRegister.PaymentTextCash("Card Checkout");
        RefreshUI();
    }

    public void Close()
    {
        _feedback?.Kill();
        IsOpen = false;
        processingPayment = false;
    }

    // ── Number Input (called by UI buttons) ───────────────────────────────────

    private bool CanUseCardPayment()
    {
        if (cashRegister == null)
        {
            Debug.LogWarning("[PaymentCard] Bloqueado: CashRegister não foi atribuído.");
            return false;
        }

        bool canUse = IsOpen && !processingPayment && cashRegister.IsUsingCardMachine;
        if (!canUse)
            Debug.LogWarning($"[PaymentCard] Bloqueado: cliente paga com {cashRegister.GetCurrentPaymentType()}, não com cartão.");

        return canUse;
    }

    public void AddNumber(string number)
    {
        if (!CanUseCardPayment()) return;
        if (currentValue.Length >= 6) return;
        currentValue += number;
        RefreshUI();
        ServiceLocator.Get<SoundManager>().Play(SFX.ClicarBotaoMaquinaCartao);
        valueText.transform.DOPunchScale(Vector3.one * 0.1f, 0.21f, 2, 0.12f);
    }

    public void Delete()
    {
        if (!CanUseCardPayment()) return;
        if (currentValue.Length == 0) return;
        currentValue = currentValue.Remove(currentValue.Length - 1);
        RefreshUI();
        ServiceLocator.Get<SoundManager>().Play(SFX.ClicarBotaoMaquinaCartao);
    }

    public void AddPoint() { /* Yen has no decimal subunit — button kept for UI completeness */ }

    public void Confirm()
    {
        if (!CanUseCardPayment()) return;
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
        if (processingPayment) return;
        processingPayment = true;

        _feedback?.Kill();
        _feedback = DOTween.Sequence()
            .Append(valueText.DOColor(Color.green, 0.2f))
            .AppendInterval(0.2f)
            .Append(valueText.DOColor(Color.black, 0.2f))
            .OnComplete(() =>
            {
                IsOpen = false;
                cashRegister.FinalizeTransaction();
            });
    }

    private void OnPaymentError()
    {
        processingPayment = true;
        currentValue = "";
        RefreshUI();
        SoundManager.Instance?.Play(SFX.Warning);
        cashRegister.ApplyPenalty();

        _feedback?.Kill();
        _feedback = DOTween.Sequence()
            .Append(valueText.DOColor(Color.red, 0.2f))
            .Join(valueText.transform.DOShakePosition(0.3f, 5.3f, 20))
            .Append(valueText.DOColor(Color.black, 0.2f))
            .OnComplete(() => processingPayment = false);
    }

    private void OnDestroy() => _feedback?.Kill();
}
