using System.Collections;
using UnityEngine;
public class DialogueTutorialState : BaseTutorialState
{
    private bool _dialogueFinished;

    public DialogueTutorialState(TutorialManager manager, TutorialStepData data) : base(manager, data) { }

    public override void Enter()
    {
        base.Enter();
        _dialogueFinished = false;

        MascotController mascot = Manager.MascotController;
        mascot.OnDialogueSequenceFinished += HandleDialogueFinished;
        mascot.MoveTo(Data.mascotTargetPosition, Data.textBoxTargetPosition);
        mascot.PlayDialogueSequence(Data.dialogueLines, Data.timePerLetterOverride);
    }

    public override void Exit()
    {
        base.Exit();
        Manager.MascotController.OnDialogueSequenceFinished -= HandleDialogueFinished;
        Manager.MascotController.ForceStopAll();
    }

       public void HandleClick()
    {
        MascotController mascot = Manager.MascotController;

        if (mascot.IsTyping)
        {
            mascot.SkipTyping();
            return;
        }

        if (Data.completionMode == TutorialCompletionMode.WaitForPlayerInput && _dialogueFinished)
        {
            Complete();
        }
    }

    private void HandleDialogueFinished()
    {
        _dialogueFinished = true;

        switch (Data.completionMode)
        {
            case TutorialCompletionMode.AutoAfterDialogue:
                Manager.StartCoroutineExternal(DelayedComplete());
                break;

            case TutorialCompletionMode.WaitForPlayerInput:
               
                break;

            case TutorialCompletionMode.WaitForGameEvent:
              
                break;
        }
    }

    private IEnumerator DelayedComplete()
    {
        yield return new WaitForSeconds(Data.delayBeforeNextStep);
        Complete();
    }
}
