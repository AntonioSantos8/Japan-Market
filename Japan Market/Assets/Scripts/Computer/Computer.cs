
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;

public class Computer : InteractableBase
{
    [SerializeField] private GameObject computerScreen;
    bool isInComputer;
    [SerializeField] CinemachineCamera computerCamera;
    [SerializeField] GameObject reticle;
    public UnityEvent onEnterComputer, onLeaveComputer;
    public override void Interact()
    {
        if (!isInComputer)
        {
            computerScreen.SetActive(true);
            computerCamera.Priority = 5;
            isInComputer = true;
            ServiceLocator.Get<PlayerMotor>().SetCanMove(false);
            ServiceLocator.Get<PlayerLook>().CanLook = false;
            reticle.SetActive(false);
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
                computerScreen.SetActive(false);
                onLeaveComputer?.Invoke();
                ServiceLocator.Get<PlayerMotor>().SetCanMove(true);
                ServiceLocator.Get<PlayerLook>().CanLook = true;
                reticle.SetActive(true);
                isInComputer = false;

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;

                ServiceLocator.Get<ShopManager>().ExitFurnitureSesion();
            }






        }
    }
}
