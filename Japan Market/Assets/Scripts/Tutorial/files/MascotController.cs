using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
public class MascotController : MonoBehaviour
{
    [SerializeField] private RectTransform mascotRoot;
    [SerializeField] private RectTransform mascotVisual;
    [SerializeField] private RectTransform textBoxRect;

    [SerializeField] private TMP_Text dialogueText;

    [SerializeField] private float idleSpeed = 2f;
    [SerializeField] private float idleAmplitude = 10f;


    [SerializeField] private float moveDuration = 0.5f;
    [SerializeField] private AnimationCurve moveEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [SerializeField] private float defaultTimePerLetter = 0.03f;

    private Vector2 _idleBasePos;
    private Coroutine _moveRoutine;
    private Coroutine _typeRoutine;
    private bool _skipRequested;

    public bool IsMoving => _moveRoutine != null;
    public bool IsTyping => _typeRoutine != null;

   
    public event Action OnDialogueSequenceFinished;

    private void Awake()
    {
        if (mascotVisual != null)
            _idleBasePos = mascotVisual.anchoredPosition;
    }

    private void Update()
    {
        if (mascotVisual == null) return;

       
        float y = Mathf.Sin(Time.time * idleSpeed) * idleAmplitude;
        mascotVisual.anchoredPosition = new Vector2(_idleBasePos.x, _idleBasePos.y + y);
    }

  
    public void MoveTo(RectTransform mascotTarget, RectTransform textBoxTarget, Vector2 textBoxTargetSize)
    {
        if (_moveRoutine != null) StopCoroutine(_moveRoutine);
        _moveRoutine = StartCoroutine(MoveRoutine(mascotTarget, textBoxTarget, textBoxTargetSize));
    }

    private IEnumerator MoveRoutine(RectTransform mascotTarget, RectTransform textBoxTarget, Vector2 textBoxTargetSize)
    {
        Vector2 startMascotPos = mascotRoot.anchoredPosition;
        Vector2 endMascotPos = mascotTarget != null ? mascotTarget.anchoredPosition : startMascotPos;

        Vector2 startBoxPos = textBoxRect.anchoredPosition;
        Vector2 endBoxPos = textBoxTarget != null ? textBoxTarget.anchoredPosition : startBoxPos;

        Vector2 startBoxSize = textBoxRect.sizeDelta;
        Vector2 endBoxSize = textBoxTargetSize;

        float t = 0f;
        while (t < moveDuration)
        {
            t += Time.deltaTime;
            float lerp = moveEase.Evaluate(Mathf.Clamp01(t / moveDuration));

            mascotRoot.anchoredPosition = Vector2.LerpUnclamped(startMascotPos, endMascotPos, lerp);
            textBoxRect.anchoredPosition = Vector2.LerpUnclamped(startBoxPos, endBoxPos, lerp);
            textBoxRect.sizeDelta = Vector2.LerpUnclamped(startBoxSize, endBoxSize, lerp);

            yield return null;
        }

        mascotRoot.anchoredPosition = endMascotPos;
        textBoxRect.anchoredPosition = endBoxPos;
        textBoxRect.sizeDelta = endBoxSize;

        _moveRoutine = null;
    }


    public void PlayDialogueSequence(DialogueLine[] lines, float timePerLetterOverride = -1f)
    {
        StopDialogueSequence();
        float speed = timePerLetterOverride > 0f ? timePerLetterOverride : defaultTimePerLetter;
        _typeRoutine = StartCoroutine(TypeSequenceRoutine(lines, speed));
    }

    public void StopDialogueSequence()
    {
        if (_typeRoutine != null)
        {
            StopCoroutine(_typeRoutine);
            _typeRoutine = null;
        }
    }

    
    public bool SkipTyping()
    {
        if (_typeRoutine == null) return false;
        _skipRequested = true;
        return true;
    }

    private IEnumerator TypeSequenceRoutine(DialogueLine[] lines, float timePerLetter)
    {
        if (lines == null || lines.Length == 0)
        {
            dialogueText.text = string.Empty;
            _typeRoutine = null;
            OnDialogueSequenceFinished?.Invoke();
            yield break;
        }

        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < lines.Length; i++)
        {
            sb.Clear();
            dialogueText.text = string.Empty;
            _skipRequested = false;

            string fullText = lines[i].text ?? string.Empty;

            for (int c = 0; c < fullText.Length; c++)
            {
                if (_skipRequested)
                {
                    sb.Clear();
                    sb.Append(fullText);
                    dialogueText.text = sb.ToString();
                    break;
                }

                sb.Append(fullText[c]);
                dialogueText.text = sb.ToString();
                yield return new WaitForSeconds(timePerLetter);
            }

            dialogueText.text = fullText;
            _skipRequested = false;

            yield return new WaitForSeconds(lines[i].extraWaitAfterLine);
        }

        _typeRoutine = null;
        OnDialogueSequenceFinished?.Invoke();
    }

    public void ForceStopAll()
    {
        if (_moveRoutine != null) { StopCoroutine(_moveRoutine); _moveRoutine = null; }
        StopDialogueSequence();
    }
}
