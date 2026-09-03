using UnityEngine;
using DG.Tweening;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class MarketManager : MonoBehaviour
{
    private bool open = false;
    private float money;
    private float late_money;
    private Tweener moneyTween;

    [SerializeField] private float clients;
    [SerializeField] private TextMeshProUGUI moneyText;

    public float Money { get => money; set => money = value; }
    public bool Open { get => open; set => open = value; }
    public float Clients { get => clients; set => clients = value; }

    private List<Transform> clientTransforms = new List<Transform>();
    public IReadOnlyList<Transform> ClientTransforms => clientTransforms;

    private Vector3 _baseScale;
    private Canvas _rootCanvas;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public void RegisterClient(Transform client)
    {
        if (!clientTransforms.Contains(client)) clientTransforms.Add(client);
    }
    public void UnregisterClient(Transform client) => clientTransforms.Remove(client);

    void Start()
    {
        ServiceLocator.Register(this);
        Earn_Money(8000);
        if (moneyText != null)
        {
            _baseScale = moneyText.transform.localScale;
            _rootCanvas = moneyText.GetComponentInParent<Canvas>()?.rootCanvas;
        }
        LoadMoney();
    }

    public void LoadMoney()
    {
        late_money = money;
        if (moneyText != null)
            moneyText.text = $"Iene: {FormatMoney(money)}";
    }

    [ContextMenu("Test Earn")]
    public void TestEarn() => Earn_Money(100000);

    // ── Earn ─────────────────────────────────────────────────────────────────

    public void Earn_Money(float quantity)
    {
        money += quantity;

        ServiceLocator.Get<SoundManager>().Play(SFX.GanharDinheiro);

        moneyTween?.Kill();
        moneyTween = DOTween.To(
            () => late_money,
            x => { late_money = x; moneyText.text = $"Iene: {FormatMoney(late_money)}"; },
            money, 0.9f).SetEase(Ease.OutExpo);

        PlayEarnVFX(quantity);
    }

    private void PlayEarnVFX(float amount)
    {
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

    public void Lose_Money(float quantity)
    {
        if (money < quantity) return;
        money -= quantity;

        moneyTween?.Kill();
        moneyTween = DOTween.To(
            () => late_money,
            x => { late_money = x; moneyText.text = $"Iene: {FormatMoney(late_money)}"; },
            money, 1f).SetEase(Ease.OutQuad);

        moneyText.DOKill();
        moneyText.transform.DOKill(true);
        DOTween.Sequence()
            .Append(moneyText.DOColor(new Color(1f, 0.28f, 0.28f), 0.07f))
            .Append(moneyText.DOColor(Color.white, 0.5f).SetEase(Ease.OutCubic));
        moneyText.transform.DOShakePosition(0.35f, new Vector3(7f, 0f, 0f), 20, 90f, false, true);
    }

    // ── Format ───────────────────────────────────────────────────────────────

    private string FormatMoney(float value)
    {
        if (value >= 1_000_000_000f) return $"¥{value / 1_000_000_000f:0.##}b";
        if (value >= 1_000_000f) return $"¥{value / 1_000_000f:0.##}m";
        if (value >= 1_000f) return $"¥{value / 1_000f:0.##}k";
        return "¥" + Mathf.FloorToInt(value);
    }
}
