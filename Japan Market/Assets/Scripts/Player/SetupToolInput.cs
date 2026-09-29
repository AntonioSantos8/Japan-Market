using JapanMarket.Gameplay;
using JapanMarket.UI;
using UnityEngine;

/// <summary>Uses the existing input owner; right click avoids the legacy left-click interaction.</summary>
[RequireComponent(typeof(PlayerInput), typeof(ToolUser))]
public sealed class SetupToolInput : MonoBehaviour
{
    [Header("Roda de ferramentas")]
    [SerializeField] private ToolWheelView wheel;
    [SerializeField] private KeyCode wheelKey = KeyCode.Tab;

    private ToolUser tools;
    private ItemRaycastController interaction;
    private PlayerInput input;
    private PlayerController controller;
    private PlayerMotor motor;
    private bool wheelOpen;
    private ToolWheelView subscribedWheel;
    private bool controllerWasEnabled;
    private bool motorWasMovable;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;

    public bool IsWheelOpen => wheelOpen;

    private void Awake()
    {
        tools = GetComponent<ToolUser>();
        input = GetComponent<PlayerInput>();
        interaction = GetComponentInChildren<ItemRaycastController>();
        controller = GetComponent<PlayerController>();
        motor = GetComponent<PlayerMotor>();
        if (wheel == null)
            wheel = FindFirstObjectByType<ToolWheelView>(FindObjectsInactive.Include);
        SubscribeWheel();
    }

    private void Update()
    {
        if (Input.GetMouseButtonUp(1)) tools.StopUsing();

        if (wheelOpen)
        {
            if (!input.enabled || Time.timeScale == 0f
                || (interaction != null && interaction.HeldItem != null))
            {
                CloseWheel(false);
                return;
            }

            wheel.UpdatePointer(Input.mousePosition);
            if (Input.GetKeyUp(wheelKey)) CloseWheel(true);
            return;
        }

        if (!input.enabled || Time.timeScale == 0f || Cursor.lockState != CursorLockMode.Locked)
            return;

        if (interaction != null && interaction.HeldItem != null) { tools.Deselect(); return; }

        if (Input.GetKeyDown(wheelKey))
        {
            OpenWheel();
            return;
        }

        if (Input.GetMouseButtonDown(1)) tools.UseOnAim();
    }

    private void OpenWheel()
    {
        tools.StopUsing();
        if (wheel == null)
            wheel = FindFirstObjectByType<ToolWheelView>(FindObjectsInactive.Include);
        SubscribeWheel();
        if (wheel == null || !wheel.Open()) return;

        wheelOpen = true;
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;

        if (controller != null)
        {
            controllerWasEnabled = controller.enabled;
            controller.enabled = false;
        }

        if (motor != null)
        {
            motorWasMovable = motor.CanMove;
            motor.SetCanMove(false);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        wheel.UpdatePointer(Input.mousePosition);
    }

    private void CloseWheel(bool commitSelection)
    {
        if (!wheelOpen) return;

        wheelOpen = false;
        if (wheel != null) wheel.Close(commitSelection);

        if (controller != null) controller.enabled = controllerWasEnabled;
        if (motor != null) motor.SetCanMove(motorWasMovable);
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
    }

    private void OnDisable()
    {
        tools?.StopUsing();
        CloseWheel(false);
        UnsubscribeWheel();
    }

    private void OnEnable() => SubscribeWheel();

    private void SubscribeWheel()
    {
        if (wheel == null || subscribedWheel == wheel) return;
        UnsubscribeWheel();
        wheel.Opened += OnWheelOpened;
        wheel.Hovered += OnWheelHovered;
        wheel.Selected += OnWheelSelected;
        wheel.Closed += OnWheelClosed;
        wheel.LockedHovered += OnWheelLocked;
        subscribedWheel = wheel;
    }

    private void UnsubscribeWheel()
    {
        if (subscribedWheel == null) return;
        subscribedWheel.Opened -= OnWheelOpened;
        subscribedWheel.Hovered -= OnWheelHovered;
        subscribedWheel.Selected -= OnWheelSelected;
        subscribedWheel.Closed -= OnWheelClosed;
        subscribedWheel.LockedHovered -= OnWheelLocked;
        subscribedWheel = null;
    }

    private void OnWheelOpened() => SoundManager.Instance?.Play(SFX.WheelOpen);
    private void OnWheelHovered() => SoundManager.Instance?.Play(SFX.WheelHover);
    private void OnWheelSelected() => SoundManager.Instance?.Play(SFX.WheelSelect);
    private void OnWheelClosed() => SoundManager.Instance?.Play(SFX.WheelClose);
    private void OnWheelLocked() => SoundManager.Instance?.Play(SFX.Warning);

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) CloseWheel(false);
    }
}
