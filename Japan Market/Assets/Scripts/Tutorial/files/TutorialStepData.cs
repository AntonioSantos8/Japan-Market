using System;
using UnityEngine;
using UnityEngine.Events;
public enum TutorialCompletionMode
{
    AutoAfterDialogue,

    WaitForPlayerInput,
    WaitForGameEvent
}

[Serializable]
public class DialogueLine
{
    [TextArea(2, 5)] public string text;

    public float extraWaitAfterLine = 1f;
}

[Serializable]
public class TutorialStepData
{
   
    public string stepId;
    public RectTransform mascotTargetPosition;

    public RectTransform textBoxTargetPosition;

    public DialogueLine[] dialogueLines;

    public UnityEvent onMascotStartTalking;

    public float timePerLetterOverride = -1f;

    public TutorialCompletionMode completionMode = TutorialCompletionMode.AutoAfterDialogue;

    public string requiredEventId;

    public float delayBeforeNextStep = 0.3f;
}
