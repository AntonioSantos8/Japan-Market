using System;
using UnityEngine;
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

    public Vector2 textBoxTargetSize = new Vector2(500, 160);

    public DialogueLine[] dialogueLines;

    public float timePerLetterOverride = -1f;

    public TutorialCompletionMode completionMode = TutorialCompletionMode.AutoAfterDialogue;

    public string requiredEventId;

    public float delayBeforeNextStep = 0.3f;
}
