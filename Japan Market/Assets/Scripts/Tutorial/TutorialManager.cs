using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using System;
using System.Collections;
using System.Text;
[Serializable]
public class MascotSettings 
{
    [SerializeField] Transform mascotMover;
    [SerializeField] Transform mascotTransform;
    [SerializeField] TMP_Text mascotText; 

    [SerializeField] float yIdleSpeed;
    [SerializeField] float yIdleAmplitude;

    float currentY = 0;
    public void Init() 
    {
        currentY = mascotTransform.localPosition.y; 
    
    }
    public void Animate() 
    {
        float yDelta = Mathf.Sin(Time.time * yIdleSpeed) + currentY;
        mascotTransform.localPosition = new Vector3(mascotTransform.localPosition.x, yDelta * yIdleAmplitude, mascotTransform.localPosition.z);
    }
    public void SetText(string txt) 
    {
        mascotText.text = txt;  
    }
}
[Serializable]
public class TutorialStepData
{
    [SerializeField] Transform mascotPosition;
    [SerializeField] DialogueData mascotDialogues;

    public DialogueData MascotDialogue { get => mascotDialogues; set => mascotDialogues = value; }
}
[Serializable]
public class DialogueData 
{
    [SerializeField] string[] dialogues;
    [SerializeField] int timeToGoToTheNextDialogue;

    public string[] Dialogues { get => dialogues; set => dialogues = value; }
    public int TimeToGoToTheNextDialogue { get => timeToGoToTheNextDialogue; set => timeToGoToTheNextDialogue = value; }
}

public class TutorialManager : MonoBehaviour
{
    [SerializeField] MascotSettings mascotSettings;
    [SerializeField] TutorialStepData awsdTutorial;
    [SerializeField] float timeForEachLetter;
    ITutorialStates currentState;
    MoveTutorialState moveState;
    private void Awake()
    {
        moveState = new MoveTutorialState(this, awsdTutorial);
        mascotSettings.Init();
        ChangeState(moveState);
    }
    public void ChangeState(ITutorialStates state) 
    {
        currentState?.Exit();
        currentState = state;
        currentState?.Enter();
    }
    private void Update()
    {
        currentState?.Update();
        mascotSettings.Animate();
    }   
    public void WriteDialogueData(TutorialStepData data) { StartCoroutine(WriteDialogue(data)); }
    IEnumerator WriteDialogue(TutorialStepData data) 
    {
        for (int i = 0; i < data.MascotDialogue.Dialogues.Length; i++)
        {
            char[] letters = data.MascotDialogue.Dialogues[i].ToCharArray();
            StringBuilder currentDialogue = new StringBuilder();
            for (int j = 0; j < letters.Length; j++) 
            {
                currentDialogue.Append(letters[j]);
                mascotSettings.SetText(currentDialogue.ToString());
                yield return new WaitForSeconds(timeForEachLetter);
                
            }
            yield return new WaitForSeconds(data.MascotDialogue.TimeToGoToTheNextDialogue);


        }
    }
    

}
