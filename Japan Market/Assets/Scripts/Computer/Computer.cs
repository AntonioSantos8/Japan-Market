
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
    TutorialManager _tutorialManager;
    private ComputerHudVisibility[] _hudViews;

    void Start()
    {
        _tutorialManager = ServiceLocator.Get<TutorialManager>();
    }
    public override void Interact()
    {
        if (!isInComputer)
        {
            if (_tutorialManager)
                _tutorialManager.NotifyGameEvent("EnteredComputer");

            ServiceLocator.Get<SoundManager>().Play(SFX.PCLigarDesligar);
            computerScreen.SetActive(true);
            computerCamera.Priority = 5;
            isInComputer = true;
            ServiceLocator.Get<PlayerMotor>().SetCanMove(false);
            ServiceLocator.Get<PlayerLook>().CanLook = false;
            reticle.SetActive(false);

            _hudViews = FindObjectsByType<ComputerHudVisibility>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (ComputerHudVisibility hud in _hudViews)
                hud.Hide();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            onEnterComputer?.Invoke();
        }
    }

    private void Update()
    {
        if (isInComputer)
        {

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ServiceLocator.Get<SoundManager>().Play(SFX.PCLigarDesligar);
                computerCamera.Priority = 0;
                computerScreen.SetActive(false);
                RestoreHud();
                onLeaveComputer?.Invoke();
                ServiceLocator.Get<PlayerMotor>().SetCanMove(true);
                ServiceLocator.Get<PlayerLook>().CanLook = true;
             
                reticle.SetActive(true);
                isInComputer = false;

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;

               
            }






        }
    }

    private void RestoreHud(bool immediate = false)
    {
        if (_hudViews == null) return;
        foreach (ComputerHudVisibility hud in _hudViews)
            if (hud != null) hud.Show(immediate);
        _hudViews = null;
    }

    private void OnDisable()
    {
        RestoreHud(immediate: true);
    }
}
