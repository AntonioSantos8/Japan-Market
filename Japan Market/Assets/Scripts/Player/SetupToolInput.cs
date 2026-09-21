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
    private bool wheelOpen;
    private bool controllerWasEnabled;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;

    public bool IsWheelOpen => wheelOpen;

    private void Awake()
    {
        tools = GetComponent<ToolUser>();
        input = GetComponent<PlayerInput>();
        interaction = GetComponentInChildren<ItemRaycastController>();
        controller = GetComponent<PlayerController>();
        if (wheel == null)
            wheel = FindFirstObjectByType<ToolWheelView>(FindObjectsInactive.Include);
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
        if (wheel == null || !wheel.Open()) return;

        wheelOpen = true;
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;

        if (controller != null)
        {
            controllerWasEnabled = controller.enabled;
            controller.enabled = false;
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
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
    }

    private void OnDisable()
    {
        tools?.StopUsing();
        CloseWheel(false);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) CloseWheel(false);
    }
}
