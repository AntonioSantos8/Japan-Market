using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Unity.VisualScripting;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

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
    private Camera itemOverlayCamera;
    private UniversalAdditionalCameraData baseCameraData;
    private bool originalPostProcessing;
    private AntialiasingMode originalAntialiasing;
    private int itemOverlayLayer = -1;
    private int originalCameraMask;
    private readonly Dictionary<Transform, int> savedLayers = new();
    private readonly Dictionary<Collider, bool> savedColliders = new();
    private readonly Dictionary<Rigidbody, (bool kinematic, bool gravity)> savedBodies = new();

    [Header("Item Box Carry")]
    [SerializeField, Min(1f)] float boxFollowSpeed = 18f;

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
    // Construction shares the mouse with normal interactions. This lock is kept
    // independent from UI locks so each system restores only its own state.
    bool buildModeInteractionBlocked;
    bool CanProcessInteractions => generalCanInteract && !buildModeInteractionBlocked;
    bool isReticleFocused;
    public void SetGeneralCanInteract(bool to)
    {
        generalCanInteract = to;

        if (!CanProcessInteractions)
            ClearLastLooked();
    }

    public void SetBuildModeInteractionBlocked(bool blocked)
    {
        if (buildModeInteractionBlocked == blocked) return;

        buildModeInteractionBlocked = blocked;
        if (blocked)
            ClearLastLooked();
    }

    float currentHoldTime;
    InteractableBase currentHoldingInteractable;
    bool waitMouseReleaseAfterInteract;
    bool placingUntilMouseRelease;
    bool takingUntilMouseRelease;
    float nextPlacementTime;
    float nextTakeTime;

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
        SetupItemOverlayCamera();
    }

    void SetupItemOverlayCamera()
    {
        itemOverlayLayer = LayerMask.NameToLayer("HeldItemOverlay");
        if (itemOverlayLayer < 0 || cam == null)
        {
            Debug.LogError("A layer HeldItemOverlay ou a câmera do jogador não foi encontrada.", this);
            return;
        }

        originalCameraMask = cam.cullingMask;
        cam.cullingMask &= ~(1 << itemOverlayLayer);

        GameObject overlayObject = new GameObject("Held Item Overlay Camera");
        overlayObject.transform.SetParent(cam.transform, false);
        itemOverlayCamera = overlayObject.AddComponent<Camera>();
        itemOverlayCamera.cullingMask = 1 << itemOverlayLayer;
        itemOverlayCamera.useOcclusionCulling = false;
        itemOverlayCamera.nearClipPlane = cam.nearClipPlane;
        itemOverlayCamera.farClipPlane = cam.farClipPlane;
        itemOverlayCamera.fieldOfView = cam.fieldOfView;

        UniversalAdditionalCameraData overlayData = overlayObject.AddComponent<UniversalAdditionalCameraData>();
        overlayData.renderType = CameraRenderType.Overlay;
        overlayData.SetRenderer(1); // Forward renderer, shared by the PC and mobile URP assets.
        baseCameraData = cam.GetUniversalAdditionalCameraData();
        List<Camera> stack = baseCameraData.cameraStack;
        if (stack == null)
        {
            Debug.LogError("O renderizador da câmera principal não suporta camera stacking.", this);
            cam.cullingMask = originalCameraMask;
            Destroy(overlayObject);
            itemOverlayCamera = null;
            baseCameraData = null;
            return;
        }

        // Render the complete camera stack through the Volume effects once.
        // Processing the Base camera first would leave the carried box untreated;
        // processing both cameras would apply effects such as Bloom twice.
        originalPostProcessing = baseCameraData.renderPostProcessing;
        originalAntialiasing = baseCameraData.antialiasing;
        overlayData.volumeLayerMask = baseCameraData.volumeLayerMask;
        overlayData.volumeTrigger = baseCameraData.volumeTrigger;
        overlayData.renderPostProcessing = originalPostProcessing;
        overlayData.antialiasing = originalAntialiasing == AntialiasingMode.TemporalAntiAliasing
            ? AntialiasingMode.SubpixelMorphologicalAntiAliasing
            : originalAntialiasing;
        overlayData.antialiasingQuality = baseCameraData.antialiasingQuality;
        baseCameraData.renderPostProcessing = false;
        baseCameraData.antialiasing = AntialiasingMode.None;
        stack.Add(itemOverlayCamera);
    }

    void OnDestroy()
    {
        if (baseCameraData == null) return;
        baseCameraData.cameraStack?.Remove(itemOverlayCamera);
        baseCameraData.renderPostProcessing = originalPostProcessing;
        baseCameraData.antialiasing = originalAntialiasing;
        if (cam != null) cam.cullingMask = originalCameraMask;
    }

    void LateUpdate()
    {
        if (itemOverlayCamera != null && cam != null)
        {
            itemOverlayCamera.fieldOfView = cam.fieldOfView;
            itemOverlayCamera.orthographic = cam.orthographic;
            itemOverlayCamera.orthographicSize = cam.orthographicSize;
        }

        if (lastBoxHeld == null || heldItem == null || boxHandPivot == null) return;

        float amount = 1f - Mathf.Exp(-boxFollowSpeed * Time.deltaTime);
        heldItem.position = Vector3.Lerp(heldItem.position, boxHandPivot.position, amount);
        heldItem.rotation = Quaternion.Slerp(heldItem.rotation, boxHandPivot.rotation, amount);
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
        if (!CanProcessInteractions) return;

        if (TryGetInteractableHit(out RaycastHit hit))
        {
            InteractableBase interactable = hit.collider.GetComponentInParent<InteractableBase>();
            if (interactable != null)
            {
                ChangeLookedInteractable(interactable);
                bool canLookAt = CanProcessInteractions && interactable.CanInteract && lastLookedInteractable.OnLookAt();
                if (!canLookAt)
                    interactable.OnLookAway();

                canInteract = canLookAt;
                ChangeNormalReticleState(canLookAt);
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
        bool canLookAt = CanProcessInteractions && inte.CanInteract && inte.OnLookAt();
        if (!canLookAt)
            inte.OnLookAway();

        canInteract = canLookAt;
        if (canLookAt)
        {
            ChangeNormalReticleState(true);
        }




    }
    private void PerformInteractionRaycast()
    {
        if (!Input.GetMouseButton(0))
            placingUntilMouseRelease = false;
        if (!Input.GetMouseButton(1))
            takingUntilMouseRelease = false;

        if (!CanProcessInteractions) return;

        if (TryGetInteractableHit(out RaycastHit hit))
        {
            InteractableBase interactable = hit.collider.GetComponentInParent<InteractableBase>();
            if (interactable != null)
            {
                if (interactable != lastLookedInteractable)
                    ChangeLookedInteractable(interactable);

                bool canLookAtNow = CanProcessInteractions && interactable.CanInteract && interactable.OnLookAt();
                if (!canLookAtNow)
                    interactable.OnLookAway();

                canInteract = canLookAtNow;
                ChangeNormalReticleState(canLookAtNow);

                if (!Input.GetMouseButton(0))
                    waitMouseReleaseAfterInteract = false;

                if (waitMouseReleaseAfterInteract)
                {
                    ResetHold();
                    return;
                }

                if (interactable is Segment segment && lastBoxHeld != null && isWithBox)
                {
                    if (Input.GetMouseButton(1))
                    {
                        if (!takingUntilMouseRelease)
                        {
                            takingUntilMouseRelease = true;
                            nextTakeTime = 0f;
                        }

                        if (canLookAtNow && segment.CanTakeIntoBox(lastBoxHeld)
                            && Time.time >= nextTakeTime)
                        {
                            segment.TakeOneItem(lastBoxHeld);
                            nextTakeTime = Time.time + segment.PlacementInterval;
                        }
                    }
                    else if (Input.GetMouseButton(0))
                    {
                        if (!placingUntilMouseRelease)
                        {
                            placingUntilMouseRelease = true;
                            nextPlacementTime = 0f;
                        }

                        if (canLookAtNow && segment.CanPlaceFromBox(lastBoxHeld)
                            && Time.time >= nextPlacementTime)
                        {
                            segment.Interact();
                            nextPlacementTime = Time.time + segment.PlacementInterval;
                        }
                    }

                    currentHoldTime = 0f;
                    currentHoldingInteractable = null;
                    holdImage.fillAmount = 0f;
                    return;
                }

                if (placingUntilMouseRelease || takingUntilMouseRelease)
                {
                    currentHoldTime = 0f;
                    currentHoldingInteractable = null;
                    holdImage.fillAmount = 0f;
                    return;
                }

                if (Input.GetMouseButton(0) && interactable.CanInteract && canInteract && CanProcessInteractions)
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
        if (heldItemRb == null || !useItemRotation || lastBoxHeld != null) return;

        Quaternion rotOffset = boxHandPivot.rotation * Quaternion.Inverse(heldItemRb.rotation);
        rotOffset.ToAngleAxis(out float angle, out Vector3 axis);

        if (angle > 180f) angle -= 360f;

        Vector3 torque = axis * angle * Mathf.Deg2Rad * followRotationSpeed;
        heldItemRb.AddTorque(torque, ForceMode.Acceleration);
    }

    private void ClearLastLooked()
    {
        // Unity's destroyed objects can still hold a managed C# reference. Clear it
        // before invoking callbacks so a destroyed interactable cannot remain stuck
        // here and throw MissingReferenceException every frame.
        InteractableBase previous = lastLookedInteractable;
        lastLookedInteractable = null;

        if (previous != null)
            previous.OnLookAway();

        ChangeNormalReticleState(false);

        waitMouseReleaseAfterInteract = false;
        ResetHold();
    }

    private void ChangeLookedInteractable(InteractableBase next)
    {
        InteractableBase previous = lastLookedInteractable;
        lastLookedInteractable = null;

        if (previous != null)
            previous.OnLookAway();

        lastLookedInteractable = next;
    }

    void HandleHeldItemInput()
    {
        if (CanProcessInteractions && heldItem != null && Input.GetKeyDown(KeyCode.G))
        {
            DropItem();
        }
    }

    public bool PickItem(Rigidbody itemRb, bool useRotationFollow = false)
    {
        if (heldItem != null || itemRb == null) return false;

        ItemBox itemBox = itemRb.GetComponentInChildren<ItemBox>();
        if (itemBox != null && (itemOverlayCamera == null || boxHandPivot == null)) return false;

        ServiceLocator.Get<SoundManager>().Play(SFX.PegarItem);

        useItemRotation = useRotationFollow;

        heldItemRb = itemRb;
        heldItem = itemRb.transform;
        heldInteractable = itemRb.GetComponentInChildren<InteractableBase>();

        var phys = heldItemRb.GetComponentInChildren<Box>();

        if (itemBox != null)
        {
            lastBoxHeld = itemBox;
            isWithBox = true;
            CaptureItemBoxTree(heldItem);
            phys?.OpenForCarry();
        }
        else if (phys != null)
        {
            phys.StartHolding(boxHandPivot);
        }
        else
        {
            var phys2 = heldItemRb.GetComponentInChildren<HoldableItem>();
            if (phys2 != null)
                phys2.StartHolding(normalPivot);
        }

        if (itemBox == null)
            heldItem.gameObject.layer = LayerMask.NameToLayer("InShelf");

        heldInteractable.OnPickEvent?.Invoke();
        heldInteractable.SetCanInteract(false);

        ClearLastLooked();
        return true;
    }

    public void DropItem()
    {
        if (heldItem == null) return;

        if (lastBoxHeld != null && !CanDropItemBox())
        {
            ServiceLocator.Get<Warnings>()?.ShowBadWarning("Can't drop the box here.");
            return;
        }

        heldItem.SetParent(null);

        var phys = heldItemRb.GetComponent<Box>();
        if (lastBoxHeld != null)
        {
            RestoreItemBoxTree(heldItem);
            savedLayers.Clear();
            savedColliders.Clear();
            savedBodies.Clear();
        }
        else if (phys != null)
        {
            phys.StopHolding();
        }
        else
        {
            var phys2 = heldItemRb.GetComponentInChildren<HoldableItem>();
            if (phys2 != null)
                phys2.StopHolding();
        }

        if (lastBoxHeld == null)
            heldItem.gameObject.layer = LayerMask.NameToLayer("Interactive");

        if (lastBoxHeld == null && Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, .8f, interactLayer))
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

    // Items may enter or leave the box while it is carried. Keep their original
    // layers and physics state so shelving them never leaves overlay-only items.
    public void RegisterItemBoxContent(ItemBox box, Transform content)
    {
        if (box == lastBoxHeld && content != null) CaptureItemBoxTree(content);
    }

    public void RestoreItemBoxContent(ItemBox box, Transform content)
    {
        if (box == lastBoxHeld && content != null) RestoreItemBoxTree(content);
    }

    void CaptureItemBoxTree(Transform root)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (!savedLayers.ContainsKey(child)) savedLayers.Add(child, child.gameObject.layer);
            child.gameObject.layer = itemOverlayLayer;
        }

        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
        {
            if (!savedColliders.ContainsKey(collider)) savedColliders.Add(collider, collider.enabled);
            collider.enabled = false;
        }

        foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true))
        {
            if (!savedBodies.ContainsKey(body)) savedBodies.Add(body, (body.isKinematic, body.useGravity));
            body.isKinematic = true;
            body.useGravity = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }

    void RestoreItemBoxTree(Transform root)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (savedLayers.TryGetValue(child, out int layer))
            {
                child.gameObject.layer = layer;
                savedLayers.Remove(child);
            }
        }

        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
        {
            if (savedColliders.TryGetValue(collider, out bool enabled))
            {
                collider.enabled = enabled;
                savedColliders.Remove(collider);
            }
        }

        foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true))
        {
            if (savedBodies.TryGetValue(body, out var state))
            {
                body.isKinematic = state.kinematic;
                body.useGravity = state.gravity;
                savedBodies.Remove(body);
            }
        }
    }

    bool CanDropItemBox()
    {
        Vector3 destination = heldItem.position;
        Vector3 direction = destination - cam.transform.position;
        float distanceToBox = direction.magnitude;
        if (distanceToBox < 0.01f) return false;

        int playerLayer = LayerMask.NameToLayer("Player");
        int mask = Physics.DefaultRaycastLayers & ~(1 << itemOverlayLayer);
        if (playerLayer >= 0) mask &= ~(1 << playerLayer);

        // The rendered box may be visible through a wall. Check the real world
        // between the camera and the intended position before restoring physics.
        if (Physics.SphereCast(cam.transform.position, 0.08f, direction / distanceToBox,
                out RaycastHit obstruction, distanceToBox, mask, QueryTriggerInteraction.Ignore)
            && obstruction.distance < distanceToBox - 0.02f)
            return false;

        BoxCollider boxCollider = heldItem.GetComponentInChildren<BoxCollider>();
        if (boxCollider == null) return false;

        Vector3 scale = boxCollider.transform.lossyScale;
        Vector3 halfSize = Vector3.Scale(boxCollider.size * 0.5f,
            new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        halfSize = Vector3.Max(halfSize - Vector3.one * 0.01f, Vector3.one * 0.01f);
        Vector3 center = boxCollider.transform.TransformPoint(boxCollider.center);
        Collider[] overlaps = Physics.OverlapBox(center, halfSize, boxCollider.transform.rotation,
            mask, QueryTriggerInteraction.Ignore);
        foreach (Collider overlap in overlaps)
        {
            if (!overlap.transform.IsChildOf(heldItem) && !overlap.transform.IsChildOf(transform.root))
                return false;
        }

        return true;
    }

    public void OnDrawGizmos()
    {
        Debug.DrawRay(transform.position, transform.forward, Color.red);
    }
}
