using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using System;
[System.Serializable]
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
}
public class TutorialManager : MonoBehaviour
{
    [SerializeField] MascotSettings mascotSettings;
    
    ITutorialStates currentState;
    MoveTutorialState moveState;
    private void Awake()
    {
        moveState = new MoveTutorialState(this);
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
    

}
