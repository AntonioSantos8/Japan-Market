using UnityEngine;
using DG.Tweening;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Domain;

/// <summary>
/// O painel de dinheiro da loja.
///
/// ATENÇÃO — esta classe deixou de ser a dona do saldo na Fase 6. O dinheiro
/// agora vive no <see cref="ILedger"/>, em Domain, junto com o livro-razão. O
/// que sobrou aqui é a APRESENTAÇÃO: o texto, o tween, o flash, as faíscas.
///
/// A API pública não mudou de propósito — <c>Money</c>, <c>Earn_Money</c>,
/// <c>Lose_Money</c> e <c>Open</c> continuam existindo com a mesma assinatura,
/// e os scripts antigos que os chamam continuam funcionando sem uma linha de
/// mudança. A diferença é que agora eles atravessam o livro-razão, então existe
/// UM saldo só, com histórico, e a venda fechada no caixa novo aparece no mesmo
/// contador.
///
/// O efeito visual não é mais disparado por quem chama: ele reage ao evento
/// <c>BalanceChanged</c>. Assim uma venda do sistema novo ganha a mesma animação
/// sem conhecer este script.
///
/// Uma diferença de comportamento vale ser registrada: antes, <c>money += q</c>
/// fazia <c>Lose_Money(-50)</c> AUMENTAR o saldo. Agora valores não positivos são
/// ignorados nos dois métodos. Se algum lugar dependia disso, dependia de um
/// acidente.
/// </summary>
public class MarketManager : MonoBehaviour
{
    private bool open = false;
    private float late_money;
    private Tweener moneyTween;

    [SerializeField] private float clients;
    [SerializeField] private TextMeshProUGUI moneyText;

    private ILedger _ledger;
    // System.IDisposable qualificado: este arquivo NÃO importa System, senão
    // `Random` ficaria ambíguo entre System.Random e UnityEngine.Random.
    private System.IDisposable _balanceSubscription;

    /// <summary>
    /// Leitura do saldo real. O setter existe só para não quebrar chamadas
    /// antigas e é deliberadamente inerte — atribuir dinheiro sem registrar de
    /// onde veio é exatamente o que o livro-razão existe para impedir.
    /// </summary>
    public float Money
    {
        get => _ledger != null ? _ledger.Balance.Yen : 0f;
        set => Debug.LogWarning(
            "[MarketManager] Atribuir Money direto não tem mais efeito. Use " +
            "Earn_Money/Lose_Money, ou ILedger.Deposit/TryWithdraw no código novo.", this);
    }

    public bool Open
    {
        get => JapanMarket.Gameplay.GameContext.Current != null &&
            JapanMarket.Gameplay.GameContext.Current.Services.TryResolve(out IGameClock clock) ? clock.StoreIsOpen : open;
        set
        {
            open = value;
            var game = JapanMarket.Gameplay.GameContext.Current;
            if (game == null || !game.Services.TryResolve(out IGameClock clock)) return;
            if (value) open = clock.TryOpenStore(); else clock.CloseStore();
        }
    }
    public float Clients { get => clients; set => clients = value; }

    private List<Transform> clientTransforms = new List<Transform>();
    public IReadOnlyList<Transform> ClientTransforms => clientTransforms;

    private Vector3 _baseScale;
    private Canvas _rootCanvas;

    private const float VfxCooldownSeconds = 0.35f;
    private float _lastVfxTime = -99f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public void RegisterClient(Transform client)
    {
        if (clientTransforms.Contains(client)) return;
        clientTransforms.Add(client);
        JapanMarket.Gameplay.GameContext.Current?.Events.Publish(new CustomerEntered(client.GetInstanceID(), client));
    }
    public void UnregisterClient(Transform client) => UnregisterClient(client, CustomerLeaveReason.NothingToBuy);

    public void UnregisterClient(Transform client, CustomerLeaveReason reason)
    {
        if (!clientTransforms.Remove(client)) return;
        JapanMarket.Gameplay.GameContext.Current?.Events.Publish(new CustomerLeft(client.GetInstanceID(), reason == CustomerLeaveReason.Purchased, reason));
    }

    void Start()
    {
        ServiceLocator.Register(this);

        if (moneyText != null)
        {
            _baseScale = moneyText.transform.localScale;
            _rootCanvas = moneyText.GetComponentInParent<Canvas>()?.rootCanvas;
        }

        // O saldo inicial não é mais dado aqui com um Earn_Money(8000): ele é o
        // saldo de abertura do livro-razão, configurado no GameContext. Somar de
        // novo aqui geraria uma "venda" fantasma de ¥8000 no relatório do dia 1.
        BindLedger();
        LoadMoney();
    }

    private void BindLedger()
    {
        var game = JapanMarket.Gameplay.GameContext.Current;
        if (game == null)
        {
            Debug.LogWarning("[MarketManager] Nenhum GameContext nesta cena. A economia " +
                             "inteira fica desligada aqui: o saldo lê ¥0 e Earn_Money/" +
                             "Lose_Money não fazem nada. Adicione um GameContext.", this);
            return;
        }

        game.Services.TryResolve(out _ledger);
        if (_ledger == null) return;

        _balanceSubscription = game.Events.Subscribe<BalanceChanged>(OnBalanceChanged);

        // Repinta na hora. Num bind tardio (cena aditiva, GameContext que entrou
        // depois do Start) o texto está em ¥0 e o late_money em 0 — sem isto, a
        // primeira movimentação animaria de zero até o saldo real, dando um
        // salto de ¥8000 que não corresponde a transação nenhuma.
        LoadMoney();
    }

    private void OnDestroy() => _balanceSubscription?.Dispose();

    /// <summary>
    /// Um único lugar reage à mudança de saldo, venha ela de onde vier: da
    /// compra de uma prateleira no código antigo ou da venda fechada no caixa
    /// novo. Antes, cada chamador disparava o próprio efeito — e quem esquecia,
    /// mexia no dinheiro sem a tela piscar.
    /// </summary>
    private void OnBalanceChanged(BalanceChanged change)
    {
        bool spending = change.Delta.IsNegative;

        // O contador sempre acompanha: é barato e é o que o jogador lê.
        AnimateTo(change.Current.Yen, spending ? 1f : 0.9f,
                  spending ? Ease.OutQuad : Ease.OutExpo);

        // O resto é caro. Cada PlayEarnVFX instancia catorze GameObjects com
        // TextMeshProUGUI, e agora TODA venda passa por aqui — com três caixas
        // atendendo seriam quarenta e dois por frame, mais três flashes dourados
        // de tela cheia sobrepostos. Um por vez, com uma folga curta.
        if (Time.unscaledTime - _lastVfxTime < VfxCooldownSeconds) return;
        _lastVfxTime = Time.unscaledTime;

        PlaySound(spending ? SFX.GastarDinheiro : SFX.GanharDinheiro);

        if (spending) PlaySpendVFX();
        else PlayEarnVFX(change.Delta.Yen);
    }

    private void AnimateTo(float target, float duration, Ease ease)
    {
        if (moneyText == null) { late_money = target; return; }

        moneyTween?.Kill();
        moneyTween = DOTween.To(
            () => late_money,
            x => { late_money = x; moneyText.text = $"Iene: {FormatMoney(late_money)}"; },
            target, duration).SetEase(ease);
    }

    public void LoadMoney()
    {
        late_money = Money;
        if (moneyText != null)
            moneyText.text = $"Iene: {FormatMoney(Money)}";
    }

    [ContextMenu("Test Earn")]
    public void TestEarn() => Earn_Money(100000);

    // ── Earn ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Entrada de dinheiro sem motivo declarado. Mantida para o código antigo;
    /// em código novo, chame <c>ILedger.Deposit</c> com a
    /// <c>TransactionReason</c> certa, senão a linha some do relatório do dia.
    /// </summary>
    public void Earn_Money(float quantity)
    {
        if (_ledger == null) { BindLedger(); if (_ledger == null) return; }

        _ledger.Deposit(JapanMarket.Core.Money.FromYen(quantity), TransactionReason.Unknown);
    }

    /// <summary>
    /// O ServiceLocator devolve null para serviço ausente (é o contrato antigo,
    /// preservado), então tocar som sem checar é NullReferenceException numa
    /// cena sem SoundManager — a Sandbox, por exemplo.
    /// </summary>
    private static void PlaySound(SFX sfx)
    {
        SoundManager sound = ServiceLocator.Get<SoundManager>();
        if (sound != null) sound.Play(sfx);
    }

    private void PlayEarnVFX(float amount)
    {
        if (moneyText == null) return;

        // 1. Flash dourado em toda a tela
        SpawnScreenFlash();

        // 2. Punch grande no texto do contador
        moneyText.transform.DOKill(true);
        moneyText.DOKill();
        moneyText.transform.DOPunchScale(_baseScale * 0.55f, 0.55f, 7, 0.45f);
        DOTween.Sequence()
            .Append(moneyText.DOColor(new Color(1f, 0.95f, 0.08f), 0.05f))
            .Append(moneyText.DOColor(Color.white, 0.7f).SetEase(Ease.OutCubic));

        // 3. Delta label com animação stamp completa
        SpawnDeltaLabel(amount);

        // 4. Sparkles em torno do texto
        SpawnUISparkles();
    }

    // ── Screen flash ──────────────────────────────────────────────────────────

    private void SpawnScreenFlash()
    {
        if (_rootCanvas == null) return;

        var go = new GameObject("EarnFlash");
        var rect = go.AddComponent<RectTransform>();
        go.transform.SetParent(_rootCanvas.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        // Vai para o fundo — não bloqueia cliques
        go.transform.SetAsFirstSibling();

        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 0.90f, 0.04f, 0f);
        img.raycastTarget = false;

        DOTween.Sequence()
            .Append(img.DOFade(0.28f, 0.05f))
            .Append(img.DOFade(0f, 0.40f).SetEase(Ease.OutQuad))
            .OnComplete(() => Destroy(go));
    }

    // ── Delta label com stamp ─────────────────────────────────────────────────

    private void SpawnDeltaLabel(float amount)
    {
        if (_rootCanvas == null || moneyText == null) return;

        var go = new GameObject("DeltaYen");
        var tmp = go.AddComponent<TextMeshProUGUI>(); // auto-adiciona RectTransform
        go.transform.SetParent(_rootCanvas.transform, false);
        go.transform.position = moneyText.transform.position;

        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(500f, 100f);

        tmp.font = moneyText.font;
        tmp.fontSize = moneyText.fontSize * 1.6f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.text = $"+{FormatMoney(amount)}";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        tmp.alpha = 0f;
        tmp.color = new Color(1f, 0.95f, 0.08f, 0f);

        go.transform.localScale = Vector3.zero;

        float startY = go.transform.position.y;

        DOTween.Sequence()
            // ── STAMP ──
            .Insert(0.00f, go.transform.DOScale(new Vector3(2.6f, 0.10f, 1f), 0.07f).SetEase(Ease.OutQuart))
            .Insert(0.00f, tmp.DOFade(1f, 0.04f))
            // ── STRETCH ──
            .Insert(0.07f, go.transform.DOScale(new Vector3(0.55f, 2.00f, 1f), 0.09f).SetEase(Ease.OutQuart))
            // ── MOLA AMORTECIDA ──
            .Insert(0.16f, go.transform.DOScale(new Vector3(1.22f, 0.80f, 1f), 0.09f).SetEase(Ease.OutSine))
            .Insert(0.25f, go.transform.DOScale(new Vector3(0.88f, 1.18f, 1f), 0.08f).SetEase(Ease.OutSine))
            .Insert(0.33f, go.transform.DOScale(new Vector3(1.08f, 0.94f, 1f), 0.07f).SetEase(Ease.OutSine))
            .Insert(0.40f, go.transform.DOScale(Vector3.one, 0.06f).SetEase(Ease.OutSine))
            // ── SOBE E SOME ──
            .Insert(0.52f, go.transform.DOMoveY(startY + 90f, 1.00f).SetEase(Ease.OutCubic))
            .Insert(0.80f, tmp.DOFade(0f, 0.55f).SetEase(Ease.InQuad))
            .OnComplete(() => Destroy(go));
    }

    // ── UI Sparkles ───────────────────────────────────────────────────────────

    private void SpawnUISparkles()
    {
        if (_rootCanvas == null || moneyText == null) return;

        string[] syms = { "¥", "★", "✦", "◆", "¥", "★" };
        Color[] colors =
        {
            new Color(1.00f, 0.95f, 0.05f),
            new Color(1.00f, 0.75f, 0.00f),
            new Color(1.00f, 1.00f, 0.40f),
            new Color(0.80f, 1.00f, 0.25f),
        };

        Vector3 origin = moneyText.transform.position;
        float scale = Screen.height / 1080f;

        for (int i = 0; i < 12; i++)
        {
            float angle = i * 30f + Random.Range(-18f, 18f);
            float rad = angle * Mathf.Deg2Rad;
            float dist = Random.Range(55f, 135f) * scale;
            float dur = Random.Range(0.45f, 0.75f);
            float delay = Random.Range(0.00f, 0.06f);
            float size = Random.Range(18f, 32f);

            var go = new GameObject($"Spark{i}");
            var tmp = go.AddComponent<TextMeshProUGUI>(); // auto-adiciona RectTransform
            go.transform.SetParent(_rootCanvas.transform, false);
            go.transform.position = origin;

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(60f, 60f);

            Color col = colors[Random.Range(0, colors.Length)];
            tmp.font = moneyText.font;
            tmp.fontSize = size;
            tmp.text = syms[Random.Range(0, syms.Length)];
            tmp.color = new Color(col.r, col.g, col.b, 0f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            Vector3 endPos = origin + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * dist;

            DOTween.Sequence().SetDelay(delay)
                .Append(tmp.DOFade(1f, 0.06f))
                .Join(go.transform.DOMove(endPos, dur).SetEase(Ease.OutCubic))
                .Insert(delay + dur * 0.25f, tmp.DOFade(0f, dur * 0.75f).SetEase(Ease.InQuad))
                .OnComplete(() => Destroy(go));
        }
    }

    // ── Lose ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Saída de dinheiro. ATENÇÃO ao comportamento herdado: sem saldo, isto NÃO
    /// desconta e NÃO avisa — e quem chamou segue achando que comprou. Está
    /// preservado de propósito para não mudar o jogo por baixo do código antigo,
    /// mas é um defeito: em código novo use <c>ILedger.TryWithdraw</c>, que
    /// devolve false, ou <c>Charge</c> para cobranças obrigatórias.
    /// </summary>
    public void Lose_Money(float quantity)
    {
        if (_ledger == null) { BindLedger(); if (_ledger == null) return; }

        JapanMarket.Core.Money amount = JapanMarket.Core.Money.FromYen(quantity);
        if (!_ledger.CanAfford(amount)) return;

        _ledger.TryWithdraw(amount, TransactionReason.Unknown);
    }

    private void PlaySpendVFX()
    {
        if (moneyText == null) return;

        moneyText.DOKill();
        moneyText.transform.DOKill(true);
        DOTween.Sequence()
            .Append(moneyText.DOColor(new Color(1f, 0.28f, 0.28f), 0.07f))
            .Append(moneyText.DOColor(Color.white, 0.5f).SetEase(Ease.OutCubic));
        moneyText.transform.DOShakePosition(0.35f, new Vector3(7f, 0f, 0f), 20, 90f, false, true);
    }

    // ── Format ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Delega ao <c>Money.ToCompactString</c>. A versão anterior devolvia
    /// "¥-4000000" para saldo negativo (todos os ifs comparavam com o valor com
    /// sinal) e estourava no <c>Mathf.FloorToInt</c> acima de ~2,1 bilhões —
    /// dois casos que passaram a ser alcançáveis agora que o livro-razão deixa o
    /// saldo ficar negativo e guarda em long.
    /// </summary>
    private string FormatMoney(float value) =>
        JapanMarket.Core.Money.FromYen(value).ToCompactString();
}
