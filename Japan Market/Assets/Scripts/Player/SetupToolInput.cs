using JapanMarket.Gameplay;
using UnityEngine;

/// <summary>Uses the existing input owner; right click avoids the legacy left-click interaction.</summary>
[RequireComponent(typeof(PlayerInput), typeof(ToolUser))]
public sealed class SetupToolInput : MonoBehaviour
{
    private ToolUser tools;
    private ItemRaycastController interaction;
    private PlayerInput input;
    private void Awake()
    {
        tools = GetComponent<ToolUser>();
        input = GetComponent<PlayerInput>();
        interaction = GetComponentInChildren<ItemRaycastController>();
    }
    private void Update()
    {
        if (!input.enabled || Time.timeScale == 0f || Cursor.lockState != CursorLockMode.Locked) return;
        if (interaction != null && interaction.HeldItem != null) { tools.Deselect(); return; }
        for (int slot = 0; slot < 5; slot++)
            if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + slot))) tools.ToggleSelection(slot);
        if (Input.GetKeyDown(KeyCode.Alpha0)) tools.Deselect();
        if (Input.GetMouseButtonDown(1)) tools.UseOnAim();
    }
}
