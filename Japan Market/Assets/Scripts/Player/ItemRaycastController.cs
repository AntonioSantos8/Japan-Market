using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Unity.VisualScripting;

public class ItemRaycastController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] float distance = 3f;
    [SerializeField] LayerMask interactLayer;
    [SerializeField] Transform boxHandPivot;
    [SerializeField] Transform normalPivot;

    [Header("Hold Interaction")]
    [SerializeField] float interactHoldTime = 1f;
    [SerializeField] Image holdImage;

    private Camera cam;
    private HoldableItem currentItem;

    private Rigidbody heldItemRb;
    private Transform heldItem;
    private InteractableBase heldInteractable;
    private InteractableBase lastLookedInteractable;
    private ItemBox lastBoxHeld;

    Tween normalReticleScleTween;

    public bool isWithBox; 
    bool canInteract = true;
    public Items currentItemType = Items.None;

    [SerializeField] float followPositionSpeed = 50f;
    [SerializeField] float followRotationSpeed = 50f;
    [SerializeField] float speedGrowRate = 6f;

    float currentFollowPosSpeed;
    float currentFollowRotSpeed;

    [SerializeField] RectTransform normalReticle;
    [SerializeField] Vector3 lookAtScale;
    Vector3 normalScale;
    [SerializeField] float reticleTweenTime;
    [SerializeField] Ease easeReticleScale;

    float currentHoldTime;
    InteractableBase currentHoldingInteractable;

    bool useItemRotation;

    public ItemBox LastBox() => lastBoxHeld;
    public Transform HeldItem => heldItem;
    public void SetCanInteract(bool value){ canInteract = value;}

    void Awake() 
    {
        ServiceLocator.Register(this);
        normalScale = normalReticle.transform.localScale;
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

        if(Input.GetKeyDown(KeyCode.Keypad9)) 
            Time.timeScale = Time.timeScale /2;
    }
    public void ReRaycast()
    {
           Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0)); 
         if (Physics.Raycast(ray, out RaycastHit hit, distance, interactLayer))
        {
            if (hit.collider.TryGetComponent(out InteractableBase interactable))
            {
                
                    lastLookedInteractable?.OnLookAway();
                    lastLookedInteractable = interactable;
                    bool canLookAt = lastLookedInteractable.OnLookAt();
                    canInteract = canLookAt;
                    if(canLookAt)
                    {
                        ChangeNormalReticleState(true);
                    }
                
            }
        }
    }
    public void ChangeNormalReticleState(bool to)
    {   
        normalReticleScleTween?.Kill();

        if(to)
            normalReticleScleTween = normalReticle.transform.DOScale(lookAtScale, reticleTweenTime).SetEase(easeReticleScale);
        else
            normalReticleScleTween = normalReticle.transform.DOScale(normalScale, reticleTweenTime).SetEase(easeReticleScale);
    }
    public void ReLook(InteractableBase inte)
    {
                    bool canLookAt = inte.OnLookAt();
                    canInteract = canLookAt;
                    if(canLookAt)
                    {
                        ChangeNormalReticleState(true);
                    }



        
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
                    bool canLookAt = lastLookedInteractable.OnLookAt();
                    canInteract = canLookAt;
                    if(canLookAt)
                    {
                        ChangeNormalReticleState(true);
                    }
                }

                if (Input.GetMouseButton(0) && canInteract)
                {

                    if (currentHoldingInteractable != interactable)
                    {
                        currentHoldingInteractable = interactable;
                        currentHoldTime = 0f;
                    }

                    currentHoldTime += Time.deltaTime;
                    holdImage.fillAmount = currentHoldTime / interactHoldTime;

                    if (currentHoldTime >= interactHoldTime)
                    {
                        interactable.Interact();
                        currentHoldTime = 0f;
                        holdImage.fillAmount = 0f;
                        currentHoldingInteractable = null;
                    }
                }
                else
                {
                    ResetHold();
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

    void ResetHold()
    {
        currentHoldTime -= Time.deltaTime * 2f;
        currentHoldTime = Mathf.Clamp(currentHoldTime, 0f, interactHoldTime);

        holdImage.fillAmount = currentHoldTime / interactHoldTime;

        if (currentHoldTime == 0f)
            currentHoldingInteractable = null;
    }

    void FollowHand()
    {
        if (heldItemRb == null || !useItemRotation) return;

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
            ChangeNormalReticleState(false);
            lastLookedInteractable = null;
        }

        ResetHold();
    }

    void HandleHeldItemInput()
    {
        if (heldItem != null && Input.GetMouseButtonDown(1))
        {
            DropItem();
        }
    }

    public bool PickItem(Rigidbody itemRb, bool useRotationFollow = false)
    {
        if(heldItem != null) return false;

        useItemRotation = useRotationFollow;

        heldItemRb = itemRb;
        heldItem = itemRb.transform;
        heldInteractable = itemRb.GetComponentInChildren<InteractableBase>();

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

        if (heldInteractable.gameObject.GetComponent<Box>()) 
        {
            lastBoxHeld = heldItem.GetComponentInChildren<ItemBox>();
            isWithBox = true;
        }

        heldItem.gameObject.layer = LayerMask.NameToLayer("InShelf");

        heldInteractable.OnPickEvent?.Invoke();
        heldInteractable.SetCanInteract(false);

        ClearLastLooked();
        return true;
    }

    public void DropItem()
    {
        if (heldItem == null) return;

        heldItem.SetParent(null);

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
        ReRaycast();
    }

    public void OnDrawGizmos()
    {
        Debug.DrawRay(transform.position, transform.forward, Color.red );
    }
}