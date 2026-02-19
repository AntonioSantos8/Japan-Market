using UnityEngine;
using static UnityEditor.Experimental.GraphView.GraphView;

[RequireComponent(typeof(PlayerInput), typeof(PlayerMotor))]
public class PlayerController : MonoBehaviour, IPlayer
{
    private PlayerInput input;
    private PlayerMotor motor;
    private PlayerLook look;
    void Awake()
    {
        input = GetComponent<PlayerInput>();
        motor = GetComponent<PlayerMotor>();
        look = GetComponent<PlayerLook>();
    }
    void Start()
    {
        CursorLock();
    }
    public void CursorLock()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    public void CursorUnlock()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    void Update()
    {
        motor.Move(input.MoveInput, input.JumpPressed, input.RunPressed);
        look.Look(input.LookInput);
    }
}