using UnityEngine;

public class ItemRaycastController : MonoBehaviour
{
    [SerializeField] float distance = 3f;
    [SerializeField] LayerMask interactLayer;

    Camera cam;
    DragRigidbody dragSystem;
    HoldableItem currentItem;

    void Start()
    {
        cam = GetComponent<Camera>();
        dragSystem = ServiceLocator.Get<DragRigidbody>();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, distance, interactLayer))
            {
                if (hit.transform.TryGetComponent(out HoldableItem item))
                {
                    currentItem = item;
                    item.BeginHold();
                    dragSystem.HandleInputBegin(Input.mousePosition);
                }
            }
        }

        if (Input.GetMouseButton(0) && currentItem != null)
        {
            dragSystem.HandleInput(Input.mousePosition);
        }

        if (Input.GetMouseButtonUp(0) && currentItem != null)
        {
            dragSystem.HandleInputEnd(Input.mousePosition);
            currentItem.EndHold();
            currentItem = null;
        }
    }
}