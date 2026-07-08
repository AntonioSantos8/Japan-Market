using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MascotController : MonoBehaviour
{
    [SerializeField] private RectTransform mascotRoot;
    [SerializeField] private RectTransform mascotVisual;
    [SerializeField] private RectTransform textBoxRect;

    [SerializeField] private TMP_Text dialogueText;

    [SerializeField] private float idleSpeed = 2f;
    [SerializeField] private float idleAmplitude = 10f;
    [SerializeField] private float idleRotationSpeed = 2f;
    [SerializeField] private float idleRotationAmplitude = 10f;


    [SerializeField] private float moveDuration = 0.5f;
    [SerializeField] private AnimationCurve moveEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [SerializeField] private float defaultTimePerLetter = 0.03f;

    private Vector2 _idleBasePos;
    private float _idleBaseZRotation;
    private Coroutine _moveRoutine;
    private Coroutine _typeRoutine;
    private bool _skipRequested;

    public bool IsMoving => _moveRoutine != null;
    public bool IsTyping => _typeRoutine != null;

   
    public event Action OnDialogueSequenceFinished;
    private Image visual;
    [SerializeField] private Sprite defaultSprite;
    [SerializeField] private Sprite[] talkingSprites;
    private int currentSpriteIndex = -1;
    [SerializeField] private float timeBetweenTalkingSprites = 0.08f;
    private Coroutine talkingCoroutine;

    private void Awake()
    {
        if (mascotVisual != null)
        {
            _idleBasePos = mascotVisual.anchoredPosition;
            _idleBaseZRotation = mascotVisual.localEulerAngles.z;
            visual = mascotVisual.GetComponent<Image>();
        }
    }

    private IEnumerator StartTalkingCoroutine()
    {
        if (visual == null || talkingSprites == null || talkingSprites.Length == 0)
            yield break;

        while (true)
        {
            int newSprite;
            if (talkingSprites.Length == 1)
            {
                newSprite = 0;
            }
            else
            {
                do
                {
                    newSprite = UnityEngine.Random.Range(0, talkingSprites.Length);
                } while (newSprite == currentSpriteIndex);
            }

            currentSpriteIndex = newSprite;
            visual.sprite = talkingSprites[currentSpriteIndex];
            yield return new WaitForSeconds(timeBetweenTalkingSprites);
        }
    }

    public void StartTalking()
    {
        if (talkingCoroutine != null)
            StopCoroutine(talkingCoroutine);

        talkingCoroutine = StartCoroutine(StartTalkingCoroutine());
    }

    public void StopTalking()
    {
        if (talkingCoroutine != null)
        {
            StopCoroutine(talkingCoroutine);
            talkingCoroutine = null;
        }

        currentSpriteIndex = -1;
        if (visual != null && defaultSprite != null)
            visual.sprite = defaultSprite;
    }

    private void Update()
    {
        if (mascotVisual == null) return;

       
        float y = Mathf.Sin(Time.time * idleSpeed) * idleAmplitude;
        mascotVisual.anchoredPosition = new Vector2(_idleBasePos.x, _idleBasePos.y + y);

        float z = Mathf.Sin(Time.time * idleRotationSpeed) * idleRotationAmplitude;
        mascotVisual.localEulerAngles = new Vector3(0f, 0f, _idleBaseZRotation + z);
    }

  
    public void MoveTo(RectTransform mascotTarget, RectTransform textBoxTarget)
    {
        if (_moveRoutine != null) StopCoroutine(_moveRoutine);
        _moveRoutine = StartCoroutine(MoveRoutine(mascotTarget, textBoxTarget));
    }

    private IEnumerator MoveRoutine(RectTransform mascotTarget, RectTransform textBoxTarget)
    {
        Vector3 startMascotPos = mascotRoot.position;
        Vector3 endMascotPos = mascotTarget != null ? mascotTarget.position : startMascotPos;

        Vector3 startBoxPos = textBoxRect.position;
        Vector3 endBoxPos = textBoxTarget != null ? textBoxTarget.position : startBoxPos;

        float t = 0f;
        while (t < moveDuration)
        {
            t += Time.deltaTime;
            float lerp = moveEase.Evaluate(Mathf.Clamp01(t / moveDuration));

            mascotRoot.position = Vector3.LerpUnclamped(startMascotPos, endMascotPos, lerp);
            textBoxRect.position = Vector3.LerpUnclamped(startBoxPos, endBoxPos, lerp);

            yield return null;
        }

        mascotRoot.position = endMascotPos;
        textBoxRect.position = endBoxPos;

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
            StopTalking();
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
            StartTalking();

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
            StopTalking();

            yield return new WaitForSeconds(lines[i].extraWaitAfterLine);
        }

        StopTalking();
        _typeRoutine = null;
        OnDialogueSequenceFinished?.Invoke();
    }

    public void ForceStopAll()
    {
        if (_moveRoutine != null) { StopCoroutine(_moveRoutine); _moveRoutine = null; }
        StopDialogueSequence();
    }

    public void SetTutorialVisible(bool visible)
    {
        if (mascotRoot != null)
            mascotRoot.gameObject.SetActive(visible);

        if (textBoxRect != null)
            textBoxRect.gameObject.SetActive(visible);
    }
}
