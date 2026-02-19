using UnityEngine;

public class PlayerInput : MonoBehaviour
{
    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool RunPressed { get; private set; }

    public bool InteractPressed { get; private set; }

    void Update()
    {
        MoveInput = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        LookInput = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        JumpPressed = Input.GetButtonDown("Jump");
        RunPressed = Input.GetKey(KeyCode.LeftShift);
        InteractPressed = Input.GetKeyDown(KeyCode.Mouse0);
    }
}