using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(NpcInstance))]
public class NpcPhrases : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text _phraseText;

    [Header("Timing")]
    [SerializeField] private float _displayDuration = 5f;
    [SerializeField] private float _fadeDuration = 0.8f;
    [SerializeField] private float _silenceDuration = 2f;

    private NpcInstance _npcInstance;
    private Coroutine _speakRoutine;
    private string _pendingPhrase;
    private bool _negativeEventFired;

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
        if (_phraseText != null) _phraseText.alpha = 0f;
        _speakRoutine = StartCoroutine(SpeakRoutine());
    }

    private void OnDestroy()
    {
        if (_phraseText != null) _phraseText.DOKill();
    }

    // ─── API pública ──────────────────────────────────────────────────────────

    /// <summary>
    /// Interrompe qualquer fala em andamento e exibe o texto imediatamente.
    /// Após exibir, o ciclo normal de falas de humor retoma sozinho.
    /// </summary>
    public void SayPhrase(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        _pendingPhrase = text;

        if (_speakRoutine != null) StopCoroutine(_speakRoutine);
        if (_phraseText != null) { _phraseText.DOKill(); _phraseText.alpha = 0f; }

        _speakRoutine = StartCoroutine(SpeakRoutine());
    }

    /// <summary>Fala uma frase aleatória associada ao evento (interrompe qualquer fala boa).
    /// Eventos negativos fazem o NPC mudar para frases angry dali em diante.</summary>
    public void SayEvent(NpcEvent evt)
    {
        if (_npcInstance.data == null) return;
        string phrase = _npcInstance.data.GetRandomEventPhrase(evt);
        if (string.IsNullOrEmpty(phrase)) return;

        if (!_negativeEventFired) _negativeEventFired = true;
        SayPhrase(phrase);
    }

    // ─── Reação a mudança de humor ────────────────────────────────────────────

    private void OnHumorChanged(NpcHumor newHumor)
    {
        string phrase = GetPhraseForCurrentHumor();
        if (!string.IsNullOrEmpty(phrase)) SayPhrase(phrase);
    }

    // ─── Ciclo de falas ───────────────────────────────────────────────────────

    private IEnumerator SpeakRoutine()
    {
        while (true)
        {
            // Frase pendente tem prioridade total — exibe imediatamente sem esperar silêncio
            if (_pendingPhrase != null)
            {
                string phrase = _pendingPhrase;
                _pendingPhrase = null;
                yield return StartCoroutine(ShowPhrase(phrase));
            }
            else
            {
                yield return new WaitForSeconds(_silenceDuration);

                // Se um evento chegou durante o silêncio, mostra ele antes da fala de humor
                if (_pendingPhrase != null) continue;

                string phrase = GetPhraseForCurrentHumor();
                if (!string.IsNullOrEmpty(phrase))
                    yield return StartCoroutine(ShowPhrase(phrase));
            }
        }
    }

    private IEnumerator ShowPhrase(string text)
    {
        if (_phraseText == null) yield break;

        _phraseText.DOKill();
        _phraseText.alpha = 0f;
        _phraseText.text = text;

        yield return _phraseText.DOFade(1f, _fadeDuration).WaitForCompletion();
        yield return new WaitForSeconds(_displayDuration);
        yield return _phraseText.DOFade(0f, _fadeDuration)
            .OnComplete(() => _phraseText.text = string.Empty)
            .WaitForCompletion();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private string GetPhraseForCurrentHumor()
    {
        if (_npcInstance.data == null) return string.Empty;
        NpcHumor humor = _negativeEventFired ? NpcHumor.Angry : _npcInstance.CurrentHumor;
        return _npcInstance.data.GetRandomPhrase(humor);
    }
}
