using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Unity.VisualScripting;

public class ItemRaycastController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] float distance = 3f;
    [SerializeField] float interactRadius = 0.35f;
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


    [SerializeField] float followRotationSpeed = 50f;
    [SerializeField] float speedGrowRate = 6f;


    [SerializeField] RectTransform normalReticle;
    [SerializeField] Vector3 lookAtScale;
    Vector3 normalScale;
    [SerializeField] float reticleTweenTime;
    [SerializeField] Ease easeReticleScale;
    bool generalCanInteract = true;
    bool isReticleFocused;
    public void SetGeneralCanInteract(bool to) { generalCanInteract = to; }

    float currentHoldTime;
    InteractableBase currentHoldingInteractable;
    bool waitMouseReleaseAfterInteract;

    bool useItemRotation;

    private static readonly RaycastHit[] _rayBuffer    = new RaycastHit[16];
    private static readonly RaycastHit[] _sphereBuffer = new RaycastHit[16];

    public ItemBox LastBox() => lastBoxHeld;
    public Transform HeldItem => heldItem;
    public void SetCanInteract(bool value) { canInteract = value; }

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

        if (Input.GetKeyDown(KeyCode.Keypad9))
            Time.timeScale = Time.timeScale / 2;
    }
    public void ReRaycast()
    {
        if (TryGetInteractableHit(out RaycastHit hit))
        {
            if (hit.collider.TryGetComponent(out InteractableBase interactable))
            {
                lastLookedInteractable?.OnLookAway();
                lastLookedInteractable = interactable;
                bool canLookAt = lastLookedInteractable.OnLookAt();
                canInteract = canLookAt;
                if (canLookAt)
                    ChangeNormalReticleState(true);
            }
        }
    }

    // NonAlloc: reutiliza buffers estáticos para não alocar todo frame.
    // Itera manualmente até count para evitar entradas vazias no buffer.
    // GetComponentInParent permite que InteractableBase fique num pai do collider.
    private bool TryGetInteractableHit(out RaycastHit hit)
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        float dist = distance * 2f;

        int rayCount = Physics.RaycastNonAlloc(ray, _rayBuffer, dist, interactLayer);
        System.Array.Sort(_rayBuffer, 0, rayCount, HitDistanceComparer.Instance);
        for (int i = 0; i < rayCount; i++)
            if (_rayBuffer[i].collider.GetComponentInParent<InteractableBase>() != null)
            { hit = _rayBuffer[i]; return true; }

        int sphereCount = Physics.SphereCastNonAlloc(ray, interactRadius, _sphereBuffer, dist, interactLayer);
        System.Array.Sort(_sphereBuffer, 0, sphereCount, HitDistanceComparer.Instance);
        for (int i = 0; i < sphereCount; i++)
            if (_sphereBuffer[i].collider.GetComponentInParent<InteractableBase>() != null)
            { hit = _sphereBuffer[i]; return true; }

        hit = default;
        return false;
    }

    private sealed class HitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
    {
        public static readonly HitDistanceComparer Instance = new();
        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }
    public void ChangeNormalReticleState(bool to)
    {
        if (isReticleFocused == to)
            return;

        isReticleFocused = to;
        normalReticleScleTween?.Kill();

        if (to)
            normalReticleScleTween = normalReticle.transform.DOScale(lookAtScale, reticleTweenTime).SetEase(easeReticleScale);
        else
            normalReticleScleTween = normalReticle.transform.DOScale(normalScale, reticleTweenTime).SetEase(easeReticleScale);
    }
    public void ReLook(InteractableBase inte)
    {
        bool canLookAt = inte.OnLookAt();
        canInteract = canLookAt;
        if (canLookAt)
        {
            ChangeNormalReticleState(true);
        }




    }
    private void PerformInteractionRaycast()
    {
        if (TryGetInteractableHit(out RaycastHit hit))
        {
            if (hit.collider.TryGetComponent(out InteractableBase interactable))
            {
                if (interactable != lastLookedInteractable)
                {
                    lastLookedInteractable?.OnLookAway();
                    lastLookedInteractable = interactable;
                }

                bool canLookAtNow = interactable.OnLookAt();
                canInteract = canLookAtNow;
                ChangeNormalReticleState(canLookAtNow);

                if (!Input.GetMouseButton(0))
                    waitMouseReleaseAfterInteract = false;

                if (waitMouseReleaseAfterInteract)
                {
                    ResetHold();
                    return;
                }

                if (Input.GetMouseButton(0) && canInteract && generalCanInteract)
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
                        waitMouseReleaseAfterInteract = true;
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

        waitMouseReleaseAfterInteract = false;
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
        if (heldItem != null) return false;

        ServiceLocator.Get<SoundManager>().Play(SFX.PegarItem);

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
            if (phys2 != null)
                phys2.StartHolding(normalPivot);
        }

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
            if (phys2 != null)
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
        Debug.DrawRay(transform.position, transform.forward, Color.red);
    }
}