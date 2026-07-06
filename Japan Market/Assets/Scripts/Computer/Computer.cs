
using Unity.Cinemachine;
using UnityEditor.Localization.Plugins.XLIFF.V20;
using UnityEngine;
using UnityEngine.Events;

public class Computer : InteractableBase
{
    [SerializeField] private GameObject computerScreen;
    bool isInComputer;
    [SerializeField] CinemachineCamera computerCamera;
    [SerializeField] GameObject reticle;
    public UnityEvent onEnterComputer, onLeaveComputer;
    TutorialManager _tutorialManager;
   
    void Start(){
        _tutorialManager = ServiceLocator.Get<TutorialManager>();
    }
    public override void Interact()
    {
        if (!isInComputer)
        {
            if(_tutorialManager)
                _tutorialManager.NotifyGameEvent("EnteredComputer");

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
