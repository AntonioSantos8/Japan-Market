using UnityEngine;

[CreateAssetMenu(fileName = "PlayerSettings", menuName = "Configs/PlayerSettings")]
public class PlayerSettings : ScriptableObject
{
    public float moveSpeed = 5f;
    public float runSpeed = 7f;
    public float mouseSensitivity = 2f;
    public float jumpForce = 6f;
    public float gravity = -9.81f;
    public bool canMove = true;
    public LayerMask groundMask;
    [Header("Footsteps")]
    public float walkStepRate = 1.5f;
    public float runStepRate = 3.0f;
}