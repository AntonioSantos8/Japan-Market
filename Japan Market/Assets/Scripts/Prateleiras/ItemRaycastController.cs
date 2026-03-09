using System.Runtime.InteropServices;
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

    InteractableBase lastLookedInteractable;
    
    public Items currentItemType = Items.None;
    public bool isWithBox;
   public  ItemBox box;
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
       print(lastLookedInteractable);
    }

    void HandleLook()
    {

       if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, distance, interactLayer))
        {
            if (hit.collider.gameObject.TryGetComponent(out InteractableBase interactable))
            {

                if (interactable != lastLookedInteractable)
                {
                   
                    if(lastLookedInteractable != null)
                    lastLookedInteractable?.OnLookAway();

                    lastLookedInteractable = interactable;
                 
                    lastLookedInteractable.OnLookAt();
                    
                }


                if (Input.GetMouseButtonDown(0))
                 {
                    lastLookedInteractable.Interact();
                 }
            }
            else
            {
             
                if(lastLookedInteractable != null)
                lastLookedInteractable?.OnLookAway();

                lastLookedInteractable = null;
            }
        }
        else
        {
                //  if(lastLookedInteractable != null)
                //  lastLookedInteractable?.OnLookAway();

                //  lastLookedInteractable = null;

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
        itemRb.gameObject.layer = 2;
        heldInteractable = itemRb.GetComponent<InteractableBase>();
        itemRb.gameObject.GetComponent<Collider>().enabled = false;
        lastLookedInteractable = null;
        heldItemRb.isKinematic = true;
        heldItemRb.useGravity = false;
        if(heldItem.gameObject.TryGetComponent(out ItemBox ib))
        {
                box = ib;


        }
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
           heldItemRb.gameObject.GetComponent<Collider>().enabled = true;
        if (heldInteractable != null)
        {
            heldInteractable.OnDropEvent?.Invoke();
            heldInteractable.SetCanInteract(true);
        }
    box = null;
        heldItemRb = null;
        heldItem = null;
        heldInteractable = null;
    }
}

