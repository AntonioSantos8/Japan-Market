using Unity.Cinemachine;
using UnityEngine;

public class CameraTilt : MonoBehaviour
{

    [SerializeField] Vector3 _maxRotationAmount;
    [SerializeField] float _rotationSpeed;
    CinemachineCamera myCamera;
    public CinemachineCamera MyCamera => myCamera;
    Vector3 _starterRotation;

    private void Awake()
    {
        _starterRotation = transform.eulerAngles;
        myCamera = GetComponent<CinemachineCamera>();

    }
    void Update()
    {

        Vector2 viewportPos = Camera.main.ScreenToViewportPoint(Input.mousePosition);
        Vector2 normalized = (viewportPos - Vector2.one * 0.5f) * 2f;

        Vector3 tilt = new Vector3(
            _maxRotationAmount.x * -normalized.y,
            _maxRotationAmount.y * normalized.x,
            0f
        );

        Quaternion target = Quaternion.Euler(_starterRotation + tilt);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * _rotationSpeed);

    }
}
