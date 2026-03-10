using UnityEngine;

public class ItemRaycastController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] float distance = 3f;
    [SerializeField] LayerMask interactLayer;
    [SerializeField] Transform boxHandPivot;

    private Camera cam;
    private DragRigidbody dragSystem;
    private HoldableItem currentItem;

    private Rigidbody heldItemRb;
    private Transform heldItem;
    private InteractableBase heldInteractable;
    private InteractableBase lastLookedInteractable;
    private ItemBox lastBoxHeld;

    private Collider[] heldItemColliders; 

    public bool isWithBox; 
    public Items currentItemType = Items.None;

    public ItemBox LastBox() => lastBoxHeld;

    void Awake() 
    {
        ServiceLocator.Register(this);
    }

    void Start()
    {
        cam = GetComponent<Camera>();
        dragSystem = ServiceLocator.Get<DragRigidbody>();
    }

    void Update()
    {
        PerformInteractionRaycast();
        HandleHeldItemInput();
    }

    private void PerformInteractionRaycast()
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0)); 
        
        if (Physics.Raycast(ray, out RaycastHit hit, distance, interactLayer))
        {
            if (hit.collider.TryGetComponent(out InteractableBase interactable))
            {
                if (interactable != lastLookedInteractable)
                {
                    lastLookedInteractable?.OnLookAway();
                    lastLookedInteractable = interactable;
                    lastLookedInteractable.OnLookAt();
                }

                if (Input.GetMouseButtonDown(0))
                {
                    if (hit.transform.TryGetComponent(out HoldableItem holdable))
                    {
                        currentItem = holdable;
                        currentItem.BeginHold();
                        dragSystem.HandleInputBegin(Input.mousePosition);
                    }
                    else 
                    {
                        interactable.Interact();
                    }
                }
            }
            else
            {
                ClearLastLooked();
            }
        }
        else
        {
            ClearLastLooked();
        }
    }

    private void ClearLastLooked()
    {
        if (lastLookedInteractable != null)
        {
            lastLookedInteractable.OnLookAway();
            lastLookedInteractable = null;
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
    }

    public void PickItem(Rigidbody itemRb)
    {
        heldItemRb = itemRb;
        heldItem = itemRb.transform;
        heldInteractable = itemRb.GetComponent<InteractableBase>();

        heldItemRb.isKinematic = true;
        heldItemRb.useGravity = false;
        
        heldItemColliders = heldItem.GetComponentsInChildren<Collider>();
        foreach(Collider col in heldItemColliders)
        {
            col.enabled = false;
        }

        if (heldInteractable.GetItemType() == Items.Box) 
        {
            lastBoxHeld = heldItem.GetComponent<ItemBox>();
            isWithBox = true;
        }

        heldItem.SetParent(boxHandPivot);
        heldItem.localPosition = Vector3.zero;
        heldItem.localRotation = Quaternion.identity;

        heldInteractable.OnPickEvent?.Invoke();
        heldInteractable.SetCanInteract(false);
        
        ClearLastLooked();
    }

    public void DropItem()
    {
        if (heldItem == null) return;

        heldItem.SetParent(null);
        heldItemRb.isKinematic = false;
        heldItemRb.useGravity = true;

        if (heldItemColliders != null)
        {
            foreach(Collider col in heldItemColliders)
            {
                col.enabled = true;
            }
        }

        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, 1, interactLayer)) 
        {
            heldItem.position = hit.point - transform.forward * 0.2f;
        }

        heldItemRb.angularVelocity = Vector3.zero;
        heldItemRb.linearVelocity = Vector3.zero;

        heldInteractable.OnDropEvent?.Invoke();
        heldInteractable.SetCanInteract(true);

        isWithBox = false; 
        lastBoxHeld = null;
        heldItemRb = null;
        heldItem = null;
        heldInteractable = null;
        heldItemColliders = null;
    }
    public void OnDrawGizmos()
    {
        Debug.DrawRay(transform.position, transform.forward, Color.red );
    }
}