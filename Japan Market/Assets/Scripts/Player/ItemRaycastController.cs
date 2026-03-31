using Unity.Cinemachine;
using UnityEngine;

public class ItemRaycastController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] float distance = 3f;
    [SerializeField] LayerMask interactLayer;
    [SerializeField] Transform boxHandPivot;
    [SerializeField] Transform normalPivot;

    private Camera cam;
    private HoldableItem currentItem;

    private Rigidbody heldItemRb;
    private Transform heldItem;
    private InteractableBase heldInteractable;
    private InteractableBase lastLookedInteractable;
    private ItemBox lastBoxHeld;

   

    public bool isWithBox; 
    bool canInteract = true;
    public Items currentItemType = Items.None;
  [SerializeField] float followPositionSpeed = 50f;
[SerializeField] float followRotationSpeed = 50f;
[SerializeField] float speedGrowRate = 6f;

float currentFollowPosSpeed;
float currentFollowRotSpeed;

    public ItemBox LastBox() => lastBoxHeld;
    public void SetCanInteract(bool value){ canInteract = value;}
    void Awake() 
    {
        ServiceLocator.Register(this);
    }

    void Start()
    {
        cam = GetComponent<Camera>();
      
    }

    void Update()
    {
        PerformInteractionRaycast();
        HandleHeldItemInput();
  
        FollowHand();
        if(Input.GetKeyDown(KeyCode.Keypad9)) Time.timeScale = Time.timeScale /2;
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
                   
                    
                        if(canInteract)
                        interactable.Interact();
                    
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

    void FollowHand()
    {
        if (heldItemRb == null || !useItemRotation) return;
        

        //Vector3 posOffset = boxHandPivot.position - heldItemRb.position;
        //heldItemRb.linearVelocity = posOffset * followPositionSpeed * Time.deltaTime;

        Quaternion rotOffset = boxHandPivot.rotation * Quaternion.Inverse(heldItemRb.rotation);
        rotOffset.ToAngleAxis(out float angle, out Vector3 axis);

        if (angle > 180f) angle -= 360f;

        Vector3 torque = axis * angle * Mathf.Deg2Rad * followRotationSpeed;
        heldItemRb.AddTorque(torque, ForceMode.Acceleration);
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
        

        if (heldItem != null && Input.GetMouseButtonDown(1))
        {
            DropItem();
        }
    }
bool useItemRotation;
    public bool PickItem(Rigidbody itemRb, bool useRotationFollow = false)
    {
        if(heldItem != null) return false;
useItemRotation = useRotationFollow;
        heldItemRb = itemRb;
        heldItem = itemRb.transform;
        heldInteractable = itemRb.GetComponentInChildren<InteractableBase>();

        //heldItemRb.isKinematic = true;
        //heldItemRb.useGravity = false;
        
        var phys = heldItemRb.GetComponentInChildren<Box>();
        
        if (phys != null)
        {
            phys.StartHolding(boxHandPivot);
        }
        else
        {
           
            var phys2 = heldItemRb.GetComponentInChildren<HoldableItem>();
            if(phys2 != null)
              phys2.StartHolding(normalPivot);
        }
        

        currentFollowPosSpeed = 5f;
currentFollowRotSpeed = 5f;
        
      // heldItem.GetComponent<Collider>().enabled = false;

        if (heldInteractable.gameObject.GetComponent<Box>()) 
        {
            lastBoxHeld = heldItem.GetComponentInChildren<ItemBox>();
            isWithBox = true;
        }
  heldItem.gameObject.layer = LayerMask.NameToLayer("InShelf");
        // heldItem.SetParent(boxHandPivot);
        // heldItem.localPosition = Vector3.zero;
        // heldItem.localRotation = Quaternion.identity; 
        heldInteractable.OnPickEvent?.Invoke();
        heldInteractable.SetCanInteract(false);
        
        ClearLastLooked();
        return true;
    }

    public void DropItem()
    {
        if (heldItem == null) return;

        heldItem.SetParent(null);
        //heldItemRb.isKinematic = false;
        //heldItemRb.useGravity = true;
        var phys = heldItemRb.GetComponent<Box>();
        if (phys != null)
        {
            phys.StopHolding();
        }
        else
        {
             var phys2 = heldItemRb.GetComponentInChildren<HoldableItem>();
            if(phys2 != null)
              phys2.StopHolding();
        }

        //heldItem.GetComponent<Collider>().enabled = true;
        heldItem.gameObject.layer = LayerMask.NameToLayer("Interactive");
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, .8f, interactLayer)) 
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

    }
    public void OnDrawGizmos()
    {
        Debug.DrawRay(transform.position, transform.forward, Color.red );
    }
}