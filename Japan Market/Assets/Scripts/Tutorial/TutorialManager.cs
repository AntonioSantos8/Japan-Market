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
    [SerializeField] RectTransform mascotPosition;
    [SerializeField] RectTransform mascotTextPosition;
    [SerializeField] DialogueData[] mascotDialogues;

    public DialogueData[] MascotDialogue { get => mascotDialogues; set => mascotDialogues = value; }
}
[Serializable]
public class DialogueData : MonoBehaviour
{
    [SerializeField] string dialogue;
    [SerializeField] int timeToGoToTheNextDialogue;

    public string Dialogue { get => dialogue; set => dialogue = value; }
    public int TimeToGoToTheNextDialogue { get => timeToGoToTheNextDialogue; set => timeToGoToTheNextDialogue = value; }
    public void WriteDialogueData(TutorialStepData data, TMP_Text mascotText, float timeForEachLetter) { StartCoroutine(WriteDialogue(data, mascotText, timeForEachLetter)); }
    IEnumerator WriteDialogue(TutorialStepData data, TMP_Text mascotText, float timeForEachLetter)
    {
        for (int i = 0; i < data.MascotDialogue.Length; i++)
        {
            char[] letters = data.MascotDialogue[i].Dialogue.ToCharArray();
            StringBuilder currentDialogue = new StringBuilder();
            for (int j = 0; j < letters.Length; j++)
            {
                currentDialogue.Append(letters[j]);
                mascotText.text = currentDialogue.ToString();
                yield return new WaitForSeconds(timeForEachLetter);

            }
            yield return new WaitForSeconds(data.MascotDialogue[i].TimeToGoToTheNextDialogue);


        }
    }
}

public class TutorialManager : MonoBehaviour
{
    [SerializeField] MascotSettings mascotSettings;
    [SerializeField] TutorialStepData awsdTutorial;
    [SerializeField] float timeForEachLetter;
    ITutorialStates currentState;
    WelcomeMenuState moveState;

    public MascotSettings MascotSettings { get => mascotSettings; set => mascotSettings = value; }

    private void Awake()
    {
        moveState = new WelcomeMenuState(this, awsdTutorial);
        MascotSettings.Init();
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
        MascotSettings.Animate();
    }   
}
