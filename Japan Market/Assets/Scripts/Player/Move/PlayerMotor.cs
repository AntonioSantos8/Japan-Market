using UnityEngine;
using Unity.Cinemachine;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour
{
    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;
    private float stepTimer = 0f;
    private float currentStepRate = 0f;

    public bool IsRunning { get; set; }
    public bool IsMoving { get; set; }

    [SerializeField] private PlayerSettings settings;
    [SerializeField] private CinemachineBasicMultiChannelPerlin noise;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundDistance = 0.4f;
    [SerializeField] private Transform cameraTiltTransform;
    [SerializeField] private float maxTilt = 10f;
    [SerializeField] private float tiltSpeed = 5f;
    [SerializeField] private CinemachineCamera cam;
    [SerializeField] private AudioClip passos;
    [SerializeField] private AudioSource passosSource;

    bool canMove = true;
    public void SetCanMove(bool value) { canMove = value; }
    private float fovValue = 60f;
    private float currentTilt = 0f;
    private float inputX = 0f;
    private Coroutine fovCoroutine;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        ServiceLocator.Register(this);
    }

    void Update()
    {
        HandleHeadBob();
        TiltCamera();
        HandleFootsteps();
    }

    public void Move(Vector2 input, bool jump, bool run)
    {
        if (!settings.canMove) return;
        if(!canMove) return;

        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, settings.groundMask);

        if (isGrounded && velocity.y < 0)
            velocity.y = -2f;

        Vector3 move = transform.right * input.x + transform.forward * input.y;

        IsMoving = isGrounded && move.magnitude > 0.1f;
        IsRunning = IsMoving && run;

        float speed = IsRunning ? settings.runSpeed : settings.moveSpeed;
        controller.Move(move * speed * Time.deltaTime);

        if (jump && isGrounded)
            velocity.y = Mathf.Sqrt(settings.jumpForce * -2f * settings.gravity);

        velocity.y += settings.gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        inputX = input.x;
    }

    private void HandleFootsteps()
    {
        if (IsMoving && isGrounded)
        {
            currentStepRate = IsRunning ? settings.runStepRate : settings.walkStepRate;

            stepTimer -= Time.deltaTime;

            if (stepTimer <= 0)
            {
                passosSource.pitch = IsRunning ? 1.1f : 1.0f;
                passosSource.PlayOneShot(passos);
                stepTimer = 1f / currentStepRate;
            }
        }
        else
        {
            stepTimer = 0f;
        }
    }

    private void HandleHeadBob()
    {
        if (noise == null) return;

        if (IsRunning)
        {
            noise.FrequencyGain = 25;
            noise.AmplitudeGain = 4f;
            StartFovCoroutine(FovUp());
        }
        else if (IsMoving)
        {
            noise.FrequencyGain = 15;
            noise.AmplitudeGain = 2f;
            StartFovCoroutine(FovDown());
        }
        else
        {
            noise.FrequencyGain = 1.8f;
            noise.AmplitudeGain = 1.4f;
            StartFovCoroutine(FovDown());
        }
    }

    private void StartFovCoroutine(IEnumerator routine)
    {
        if (fovCoroutine != null) StopCoroutine(fovCoroutine);
        fovCoroutine = StartCoroutine(routine);
    }

    IEnumerator FovUp()
    {
        while (fovValue < 70f)
        {
            fovValue += 0.1f;
            cam.Lens.FieldOfView = fovValue;
            yield return new WaitForSeconds(0.01f);
        }
    }

    IEnumerator FovDown()
    {
        while (fovValue > 60f)
        {
            fovValue -= 0.1f;
            cam.Lens.FieldOfView = fovValue;
            yield return new WaitForSeconds(0.01f);
        }
    }

    private void TiltCamera()
    {
        if (cameraTiltTransform == null) return;

        float targetTilt = -inputX * maxTilt;
        currentTilt = Mathf.Lerp(currentTilt, targetTilt, Time.deltaTime * tiltSpeed);
        Vector3 currentEuler = cameraTiltTransform.localRotation.eulerAngles;
        cameraTiltTransform.localRotation = Quaternion.Euler(currentEuler.x, currentEuler.y, currentTilt);
    }
}