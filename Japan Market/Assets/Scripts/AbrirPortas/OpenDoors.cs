using UnityEngine;


public class OpenDoors : MonoBehaviour
{
    [SerializeField] float forcePush = 170f;
    [SerializeField] float angleDistance = 180f;
    [SerializeField] float interactDistance = 2.5f;

    float currentAngle;
    bool isDragging;
    Camera cam;

    void Start()
    {
        cam = Camera.main;
        currentAngle = transform.localEulerAngles.y;
    }

    void Update()
    {
        HandleInput();
    }

    void HandleInput()
    {
        if (Input.GetButton("Fire1"))
        {
            if (!isDragging)
            {
                TryStartDrag();
            }
            else
            {
                DragDoor();
            }
        }
        else
        {
            isDragging = false;
        }
    }

    void TryStartDrag()
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactDistance))
        {
            if (hit.transform == transform)
            {
                isDragging = true;
            }
        }
    }

    void DragDoor()
    {
        float mouseMovement = -Input.GetAxis("Mouse X");

        currentAngle += mouseMovement * forcePush * Time.deltaTime;
        currentAngle = Mathf.Clamp(currentAngle, -angleDistance, angleDistance);

        transform.localRotation = Quaternion.Euler(0, currentAngle, 0);
    }
}