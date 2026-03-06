using UnityEngine;

public class ItemRaycastController : MonoBehaviour
{
    [SerializeField] float distance = 3f;
    [SerializeField] LayerMask interactLayer;
    [SerializeField] Transform handPivot;
    [SerializeField] Transform boxHandPivot;

    Camera cam;
    DragRigidbody dragSystem;

    HoldableItem currentItem;

    Rigidbody heldItemRb;
    Transform heldItem;
    InteractableBase heldInteractable;

    InteractableBase currentLookInteractable;
    
    public Items currentItemType = Items.None;
    public bool isWithBox;

    void Start()
    {
        cam = GetComponent<Camera>();
        dragSystem = ServiceLocator.Get<DragRigidbody>();
        ServiceLocator.Register(this);
    }

    void Update()
    {
        HandleLook();
        HandleRaycast();
        HandleHeldItemInput();
    }

    void HandleLook()
{
    Ray ray = new Ray(cam.transform.position, cam.transform.forward);
    RaycastHit hit;

    if (Physics.Raycast(ray, out hit, distance, interactLayer))
    {
        InteractableBase interactable = hit.transform.GetComponentInParent<InteractableBase>();

        if (interactable != null)
        {
            if (currentLookInteractable != interactable)
            {
                if (currentLookInteractable != null)
                    currentLookInteractable.OnLookAway();

                currentLookInteractable = interactable;
                currentLookInteractable.OnLookAt();
            }

            return;
        }
    }

    if (currentLookInteractable != null)
    {
        currentLookInteractable.OnLookAway();
        currentLookInteractable = null;
    }
}
    void HandleRaycast()
    {
        if (!Input.GetMouseButtonDown(0)) return;

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

            if (hit.transform.TryGetComponent(out InteractableBase interactable))
            {
                interactable.Interact();
            }

            if (hit.rigidbody != null && heldItem == null)
            {
                PickItem(hit.rigidbody);
            }
        }
    }

    void HandleHeldItemInput()
    {
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

        if (heldItem != null && Input.GetMouseButtonDown(1))
        {
            DropItem();
        }

        if (Input.GetKeyDown(KeyCode.Keypad9))
        {
            Time.timeScale /= 2;
        }
    }

    public void PickItem(Rigidbody itemRb)
    {
        heldItemRb = itemRb;
        heldItem = itemRb.transform;

        heldInteractable = itemRb.GetComponent<InteractableBase>();

        heldItemRb.isKinematic = true;
        heldItemRb.useGravity = false;

        heldItem.SetParent(boxHandPivot);
        heldItem.localPosition = Vector3.zero;
        heldItem.localRotation = Quaternion.identity;

        if (heldInteractable != null)
        {
            heldInteractable.OnPickEvent?.Invoke();
            heldInteractable.SetCanInteract(false);
        }
    }

    public void DropItem()
    {
        if (heldItem == null) return;

        heldItem.SetParent(null);

        heldItemRb.isKinematic = false;
        heldItemRb.useGravity = true;

        if (heldInteractable != null)
        {
            heldInteractable.OnDropEvent?.Invoke();
            heldInteractable.SetCanInteract(true);
        }

        heldItemRb = null;
        heldItem = null;
        heldInteractable = null;
    }
}