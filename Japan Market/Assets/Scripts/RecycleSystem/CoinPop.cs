using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Attach este script no prefab do pop de moeda.
/// Chame Setup(amount) logo após Instantiate para personalizar o valor.
/// Se não chamar Setup, usa o texto já configurado no prefab.
/// </summary>
public class CoinPop : MonoBehaviour
{
    [SerializeField] private TMP_Text _label;

    [Header("Timing")]
    [SerializeField] private float _popDuration   = 0.22f;
    [SerializeField] private float _floatDuration = 1.1f;
    [SerializeField] private float _floatHeight   = 1.6f;

    [Header("Cores")]
    [SerializeField] private Color _flashColor  = new Color(1f, 0.95f, 0.3f);   // amarelo flash
    [SerializeField] private Color _targetColor = new Color(0.25f, 1f, 0.45f);  // verde moeda

    private void Awake()
    {
        if (_label == null) _label = GetComponentInChildren<TMP_Text>(true);
    }

    /// <summary>Define o valor exibido no pop antes de Start rodar.</summary>
    public void Setup(float amount) => _label.text = $"+¥{Mathf.FloorToInt(amount)}";

    private void Start()
    {
        _label.alpha = 0f;
        _label.color = _flashColor;
        transform.localScale = Vector3.zero;

        Vector3 startY = transform.position;

        Sequence s = DOTween.Sequence().SetAutoKill(true);

        // ── Fase 1: aparição (0 → popDuration) ───────────────────────────────
        // Escala de 0 a 1.3 com overshoot elástico, depois acomoda em 1.0
        s.Insert(0f, transform.DOScale(1.3f, _popDuration * 0.55f)
            .SetEase(Ease.OutBack, 4f));
        s.Insert(_popDuration * 0.55f, transform.DOScale(1f, _popDuration * 0.45f)
            .SetEase(Ease.OutSine));

        // Fade in rápido
        s.Insert(0f, _label.DOFade(1f, _popDuration * 0.4f));

        // Flash de cor: amarelo brilhante → verde moeda
        s.Insert(0f, _label.DOColor(_targetColor, _popDuration * 0.8f)
            .SetEase(Ease.OutCubic));

        // ── Fase 2: punch de alegria logo após o pop ──────────────────────────
        s.Insert(_popDuration, transform.DOPunchScale(
            punch: new Vector3(0.3f, -0.15f, 0.3f),
            duration: 0.3f,
            vibrato: 4,
            elasticity: 0.5f));

        // ── Fase 3: flutua pra cima (popDuration → popDuration + floatDuration) ─
        float floatStart = _popDuration + 0.05f;
        s.Insert(floatStart, transform.DOMoveY(startY.y + _floatHeight, _floatDuration)
            .SetEase(Ease.OutCubic));

        // Leve rotação de -8 a +8 graus enquanto sobe — dá vida ao pop
        s.Insert(floatStart, transform.DORotate(new Vector3(0f, 0f, 8f), _floatDuration * 0.35f)
            .SetEase(Ease.OutSine));
        s.Insert(floatStart + _floatDuration * 0.35f, transform.DORotate(new Vector3(0f, 0f, -5f), _floatDuration * 0.3f)
            .SetEase(Ease.InOutSine));
        s.Insert(floatStart + _floatDuration * 0.65f, transform.DORotate(Vector3.zero, _floatDuration * 0.35f)
            .SetEase(Ease.InSine));

        // ── Fase 4: fade out + encolhe na segunda metade do float ────────────
        float fadeStart = floatStart + _floatDuration * 0.48f;
        s.Insert(fadeStart, _label.DOFade(0f, _floatDuration * 0.52f)
            .SetEase(Ease.InQuad));
        s.Insert(fadeStart, transform.DOScale(0.65f, _floatDuration * 0.52f)
            .SetEase(Ease.InSine));

        s.OnComplete(() => Destroy(gameObject));
    }
}
