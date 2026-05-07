using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Exibe frases do NPC de acordo com o humor atual.
/// Escuta o evento OnHumorChanged do NpcInstance para reagir imediatamente
/// quando o humor muda (ex: NPC fica bravo após troco errado).
/// </summary>
[RequireComponent(typeof(NpcInstance))]
public class NpcPhrases : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text _phraseText;

    [Header("Timing")]
    [SerializeField] private float _displayDuration = 5f;
    [SerializeField] private float _fadeDuration    = 0.8f;
    [SerializeField] private float _silenceDuration = 2f;

    private NpcInstance _npcInstance;
    private Coroutine _speakRoutine;

    private void Awake()
    {
        _npcInstance = GetComponent<NpcInstance>();
    }

    private void OnEnable()
    {
        _npcInstance.OnHumorChanged += OnHumorChanged;
    }

    private void OnDisable()
    {
        _npcInstance.OnHumorChanged -= OnHumorChanged;
    }

    private void Start()
    {
        // Garante que o texto começa invisível
        if (_phraseText != null)
            _phraseText.alpha = 0f;

        _speakRoutine = StartCoroutine(SpeakRoutine());
    }

    // ─── Reação imediata a mudança de humor ───────────────────────────────────

    /// <summary>
    /// Quando o humor muda, reinicia o ciclo de fala imediatamente
    /// para que o NPC diga algo condizente com o novo estado.
    /// </summary>
    private void OnHumorChanged(NpcHumor newHumor)
    {
        if (_speakRoutine != null)
            StopCoroutine(_speakRoutine);

        _speakRoutine = StartCoroutine(SpeakOnce(immediate: true));
    }

    // ─── Ciclo de falas ───────────────────────────────────────────────────────

    private IEnumerator SpeakRoutine()
    {
        while (true)
            yield return StartCoroutine(SpeakOnce(immediate: false));
    }

    private IEnumerator SpeakOnce(bool immediate)
    {
        if (!immediate)
            yield return new WaitForSeconds(_silenceDuration);

        string phrase = GetPhraseForCurrentHumor();
        if (string.IsNullOrEmpty(phrase))
        {
            yield return new WaitForSeconds(_displayDuration);
            yield break;
        }

        _phraseText.text = phrase;
        _phraseText.DOKill();
        yield return _phraseText.DOFade(1f, _fadeDuration).WaitForCompletion();

        yield return new WaitForSeconds(_displayDuration);

        yield return _phraseText.DOFade(0f, _fadeDuration)
            .OnComplete(() => _phraseText.text = "")
            .WaitForCompletion();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private string GetPhraseForCurrentHumor()
    {
        if (_npcInstance.data == null) return string.Empty;
        return _npcInstance.data.GetRandomPhrase(_npcInstance.CurrentHumor);
    }
}