using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Physical card machine on the counter. Interacting with it opens the card
/// payment (if the customer is paying by card) and switches the active
/// Cinemachine camera to a close-up of the machine. CashRegister handles
/// returning to the register camera on ESC or on a successful payment.
/// </summary>
public class CardMachine : InteractableBase
{
    [SerializeField] private CinemachineCamera machineCamera;
    [SerializeField] private int               machineCameraPriority = 15;
    [SerializeField] private CashRegister      cashRegister;

    public override void Interact()
    {
        cashRegister.EnterCardMachineMode(machineCamera, machineCameraPriority);
    }
}
