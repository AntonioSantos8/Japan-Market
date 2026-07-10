using DG.Tweening;
using TMPro;
using UnityEngine;

public class MoneyLosePop : MonoBehaviour
{
    [SerializeField] private TMP_Text _label;

    [Header("Movimento")]
    [SerializeField] private float _floatHeight   = 1.9f;
    [SerializeField] private float _floatDuration = 1.35f;

    [Header("Cores")]
    [SerializeField] private Color _flashWhite = Color.white;
    [SerializeField] private Color _redMid     = new Color(1.00f, 0.35f, 0.35f);
    [SerializeField] private Color _redFinal   = new Color(1.00f, 0.10f, 0.10f);

    [Header("Sparkles")]
    [SerializeField] private int   _sparkleCount  = 8;
    [SerializeField] private float _sparkleRadius = 0.70f;
    [SerializeField] private float _sparkleSize   = 0.08f;

    private void Awake()
    {
        if (_label == null) _label = GetComponentInChildren<TMP_Text>(true);
    }

    public void Setup(float amount) => _label.text = $"-¥{Mathf.FloorToInt(amount)}";

    private Vector3 _baseEuler;

    private void Start()
    {
        _baseEuler = transform.eulerAngles;

        _label.alpha = 0f;
        _label.color = _flashWhite;
        transform.localScale = Vector3.zero;

        SpawnEchoRing();
        SpawnSparkles();
        PlayMainSequence();
    }

    private void PlayMainSequence()
    {
        float floatEndY = transform.position.y + _floatHeight;

        Sequence s = DOTween.Sequence().SetAutoKill(true);

        s.Insert(0.000f, transform.DOScale(new Vector3(2.8f, 0.12f, 1f), 0.070f).SetEase(Ease.OutQuart));
        s.Insert(0.000f, _label.DOFade(1f, 0.040f));
        s.Insert(0.000f, _label.DOColor(_flashWhite, 0f));

        s.Insert(0.070f, transform.DOScale(new Vector3(0.50f, 2.1f, 1f), 0.100f).SetEase(Ease.OutQuart));
        s.Insert(0.070f, _label.DOColor(_redMid, 0.065f));

        s.Insert(0.17f, transform.DOScale(new Vector3(1.28f, 0.78f, 1f), 0.100f).SetEase(Ease.OutSine));
        s.Insert(0.27f, transform.DOScale(new Vector3(0.82f, 1.22f, 1f), 0.090f).SetEase(Ease.OutSine));
        s.Insert(0.36f, transform.DOScale(new Vector3(1.10f, 0.92f, 1f), 0.075f).SetEase(Ease.OutSine));
        s.Insert(0.44f, transform.DOScale(new Vector3(0.95f, 1.06f, 1f), 0.060f).SetEase(Ease.OutSine));
        s.Insert(0.50f, transform.DOScale(Vector3.one,              0.055f).SetEase(Ease.OutSine));
        s.Insert(0.22f, _label.DOColor(_redFinal, 0.22f).SetEase(Ease.OutCubic));

        // Shake horizontal enquanto sobe — transmite a sensação de "erro"
        const float fs = 0.58f;
        s.Insert(fs, transform.DOMoveY(floatEndY, _floatDuration).SetEase(Ease.OutCubic));
        s.Insert(fs + 0.06f, transform.DOShakePosition(0.35f, new Vector3(0.06f, 0f, 0f), 18, 0f, false, true));

        s.Insert(fs,                          transform.DORotate(_baseEuler + new Vector3(0, 0, -7f), _floatDuration * 0.30f).SetEase(Ease.OutSine));
        s.Insert(fs + _floatDuration * 0.30f, transform.DORotate(_baseEuler + new Vector3(0, 0,  5f), _floatDuration * 0.38f).SetEase(Ease.InOutSine));
        s.Insert(fs + _floatDuration * 0.68f, transform.DORotate(_baseEuler,                          _floatDuration * 0.32f).SetEase(Ease.InSine));

        float fadeStart = fs + _floatDuration * 0.44f;
        s.Insert(fadeStart, _label.DOFade(0f, _floatDuration * 0.56f).SetEase(Ease.InQuad));
        s.Insert(fadeStart, transform.DOScale(0.50f, _floatDuration * 0.56f).SetEase(Ease.InSine));

        s.OnComplete(() => Destroy(gameObject));
    }

    private void SpawnEchoRing()
    {
        for (int ring = 0; ring < 2; ring++)
        {
            float ringDelay  = ring * 0.07f;
            float startAlpha = ring == 0 ? 0.65f : 0.35f;

            var go = CreateTmpInWorld("Ring_" + ring, "◯", 5f, new Color(1f, 0.15f, 0.15f, startAlpha));
            go.transform.localScale = Vector3.one * 0.01f;

            Sequence rs = DOTween.Sequence().SetDelay(ringDelay).SetAutoKill(true);
            rs.Insert(0f, go.transform.DOScale(ring == 0 ? 1.6f : 2.2f, 0.42f).SetEase(Ease.OutCubic));
            rs.Insert(0f, go.GetComponent<TextMeshPro>().DOFade(0f, 0.42f).SetEase(Ease.OutQuad));
            rs.OnComplete(() => Destroy(go));
        }
    }

    private void SpawnSparkles()
    {
        string[] symbols = { "✕", "✗", "×", "✖", "✕", "✗" };
        Color[]  colors  =
        {
            new Color(1.00f, 0.10f, 0.10f), // vermelho vivo
            new Color(1.00f, 0.40f, 0.40f), // rosa-vermelho
            new Color(0.85f, 0.00f, 0.00f), // vermelho escuro
            new Color(1.00f, 1.00f, 1.00f), // branco
            new Color(1.00f, 0.25f, 0.25f), // vermelho médio
            new Color(0.60f, 0.00f, 0.00f), // bordô
        };

        for (int i = 0; i < _sparkleCount; i++)
        {
            float angle = i * (360f / _sparkleCount) + Random.Range(-22f, 22f);
            float dist  = Random.Range(0.28f, _sparkleRadius);
            float delay = Random.Range(0.00f, 0.065f);
            float dur   = Random.Range(0.40f, 0.68f);
            float scale = _sparkleSize * Random.Range(0.75f, 1.4f);

            string sym = symbols[Random.Range(0, symbols.Length)];
            Color  col = colors [Random.Range(0, colors.Length)];

            var go = CreateTmpInWorld($"Spark{i}", sym, 36f, col);
            go.transform.localScale = Vector3.zero;

            float rad   = angle * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
            Vector3 end = transform.position + dir * dist;
            float rotZ  = Random.Range(-280f, 280f);

            Sequence ss = DOTween.Sequence().SetDelay(delay).SetAutoKill(true);
            ss.Insert(0f, go.GetComponent<TextMeshPro>().DOFade(1f, 0.055f));
            ss.Insert(0f, go.transform.DOScale(scale, 0.09f).SetEase(Ease.OutBack, 2.5f));
            ss.Insert(0f, go.transform.DOMove(end, dur).SetEase(Ease.OutCubic));
            ss.Insert(0f, go.transform.DORotate(new Vector3(0, 0, rotZ), dur, RotateMode.FastBeyond360));
            ss.Insert(dur * 0.22f, go.GetComponent<TextMeshPro>().DOFade(0f, dur * 0.78f).SetEase(Ease.InQuad));
            ss.Insert(dur * 0.45f, go.transform.DOScale(0f, dur * 0.55f).SetEase(Ease.InBack));
            ss.OnComplete(() => Destroy(go));
        }
    }

    private GameObject CreateTmpInWorld(string goName, string text, float fontSize, Color color)
    {
        var go = new GameObject(goName);
        go.transform.position = transform.position;
        go.transform.rotation = Camera.main != null ? Camera.main.transform.rotation : Quaternion.identity;

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text         = text;
        tmp.fontSize     = fontSize;
        tmp.color        = color;
        tmp.alignment    = TextAlignmentOptions.Center;
        tmp.sortingOrder = 100;
        return go;
    }
}
