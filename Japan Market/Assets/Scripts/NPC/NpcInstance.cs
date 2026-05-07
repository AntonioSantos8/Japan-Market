using System;
using UnityEngine;

public enum PaymentType { Cash, Card }

/// <summary>
/// Núcleo do NPC: armazena dados de identidade, tipo de pagamento e o sistema
/// de humor por moedas. Expõe um evento para que outros componentes (ex: NpcPhrases)
/// reajam à mudança de humor sem acoplamento direto.
/// </summary>
[RequireComponent(typeof(NpcPhrases))]
public class NpcInstance : MonoBehaviour
{
    [Header("Data")]
    public NpcData data;

    [Header("Payment")]
    public PaymentType paymentType;

    [Header("Humor System")]
    [SerializeField] private int _humorCoins = 100;
    [SerializeField] private int _maxHumorCoins = 100;

    // Thresholds configuráveis pelo designer no Inspector
    [SerializeField] private int _happyThreshold  = 70;
    [SerializeField] private int _neutralThreshold = 30;

    public NpcHumor CurrentHumor { get; private set; } = NpcHumor.Happy;
    public int HumorCoins => _humorCoins;

    /// <summary>Disparado sempre que o humor muda de estado.</summary>
    public event Action<NpcHumor> OnHumorChanged;

    private void Awake()
    {
        paymentType = (UnityEngine.Random.value > 0.5f) ? PaymentType.Card : PaymentType.Cash;
    }

    // ─── Humor API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Chamado quando o jogador dá troco errado ao NPC.
    /// Penaliza as moedas de humor e recalcula o estado.
    /// </summary>
    public void ReceiveWrongChange(int penalty)
    {
        ModifyHumorCoins(-penalty);
        Debug.Log($"[NPC] Troco errado! Moedas: {_humorCoins}. Humor: {CurrentHumor}");
    }

    /// <summary>
    /// Chamado quando o jogador age bem (ex: troco correto, atendimento rápido).
    /// </summary>
    public void ReceivePositiveInteraction(int bonus)
    {
        ModifyHumorCoins(bonus);
    }

    private void ModifyHumorCoins(int delta)
    {
        _humorCoins = Mathf.Clamp(_humorCoins + delta, 0, _maxHumorCoins);
        RefreshHumorState();
    }

    private void RefreshHumorState()
    {
        NpcHumor newHumor;

        if (_humorCoins >= _happyThreshold)
            newHumor = NpcHumor.Happy;
        else if (_humorCoins >= _neutralThreshold)
            newHumor = NpcHumor.Neutral;
        else
            newHumor = NpcHumor.Angry;

        if (newHumor == CurrentHumor) return;

        CurrentHumor = newHumor;

        // Propaga mudança para quem escuta (ex: NpcPhrases, UI de debug, etc.)
        OnHumorChanged?.Invoke(CurrentHumor);
    }
}