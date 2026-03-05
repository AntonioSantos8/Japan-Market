using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;

public class Computer : MonoBehaviour, IInteractable
{
    bool isInComputer;
    [SerializeField] CinemachineCamera computerCamera;
    public UnityEvent onEnterComputer, onLeaveComputer;
    public void Interact()
    {
        if (!isInComputer) 
        {
            computerCamera.Priority = 5;
            isInComputer = true;
        ServiceLocator.Get<PlayerMotor>().SetCanMove(false);
            ServiceLocator.Get<PlayerLook>().CanLook = false;
            onEnterComputer?.Invoke();
        }
    }

    private void Update()
    {
        if (isInComputer) 
        {

            if (Input.GetKeyDown(KeyCode.Escape)) 
            {
                computerCamera.Priority = 0;

                onLeaveComputer?.Invoke();
                ServiceLocator.Get<PlayerMotor>().SetCanMove(true);
                ServiceLocator.Get<PlayerLook>().CanLook = true;
                isInComputer=false;

            }
           





        }   
    }
}
