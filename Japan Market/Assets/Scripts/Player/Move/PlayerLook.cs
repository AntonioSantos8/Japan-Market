using UnityEditor;
using UnityEngine;
using UnityEngine.Android;

public class PlayerLook : MonoBehaviour
{
    [SerializeField] private Transform playerBody;
    [SerializeField] private Transform cameraHolder;
    [SerializeField] private PlayerSettings settings;

    [Header("Rotation Restrictions")]
    [SerializeField] private bool useRotationRestrictions;
    [SerializeField] private float minXRotation = -90f;
    [SerializeField] private float maxXRotation = 90f;
    [SerializeField] private float minYRotation = 0f;
    [SerializeField] private float maxYRotation = 360f;

    public bool CanLook { get; set; } = true;
    public float xRotation = 0f;
    private void Awake()
    {
        ServiceLocator.Register(this);
    }
    public void Look(Vector2 input)
    {
        if (!CanLook) { return; }

        float mouseX = input.x * settings.mouseSensitivity;
        float mouseY = input.y * settings.mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation,
            useRotationRestrictions ? minXRotation : -90f,
            useRotationRestrictions ? maxXRotation : 90f);

        cameraHolder.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        float targetY = playerBody.eulerAngles.y + mouseX;
        playerBody.rotation = Quaternion.Euler(0f,
            useRotationRestrictions ? ClampYRotation(targetY) : targetY,
            0f);
    }

    public void SetRotationRestrictions(float minimumX, float maximumX, float minimumY, float maximumY)
    {
        minXRotation = minimumX;
        maxXRotation = maximumX;
        minYRotation = minimumY;
        maxYRotation = maximumY;
        useRotationRestrictions = true;

        xRotation = Mathf.Clamp(xRotation, minXRotation, maxXRotation);
        cameraHolder.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        playerBody.rotation = Quaternion.Euler(0f, ClampYRotation(playerBody.eulerAngles.y), 0f);
    }

    public void ClearRotationRestrictions()
    {
        useRotationRestrictions = false;
    }

    private float ClampYRotation(float angle)
    {
        angle = Mathf.Repeat(angle, 360f);
        if (Mathf.Abs(maxYRotation - minYRotation) >= 360f) return angle;

        float min = Mathf.Repeat(minYRotation, 360f);
        float max = Mathf.Repeat(maxYRotation, 360f);

        if (min <= max) return Mathf.Clamp(angle, min, max);
        if (angle >= min || angle <= max) return angle;

        return Mathf.Abs(Mathf.DeltaAngle(angle, min)) < Mathf.Abs(Mathf.DeltaAngle(angle, max))
            ? min
            : max;
    }
    public void ResetLook()
    {
        xRotation = 0f;
      cameraHolder.localRotation = Quaternion.Euler(0f, 0f, 0f);

      playerBody.rotation = Quaternion.Euler(0f, 272f, 0f);
    }

}
