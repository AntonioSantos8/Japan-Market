using UnityEditor;
using UnityEngine;

public class PlayerLook : MonoBehaviour
{
    [SerializeField] private Transform playerBody;
    [SerializeField] private Transform cameraHolder;
    [SerializeField] private PlayerSettings settings;
    public bool CanLook { get; set; } = true;
    public float xRotation = 0f;

    public void Look(Vector2 input)
    {
        if (!CanLook) { return; }

        float mouseX = input.x * settings.mouseSensitivity;
        float mouseY = input.y * settings.mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        cameraHolder.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        playerBody.Rotate(Vector3.up * mouseX);
    }

}