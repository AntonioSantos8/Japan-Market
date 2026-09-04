using DG.Tweening;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FurnitureManager : MonoBehaviour
{
    [SerializeField] private List<FurnitureData> availableFurniture;
    [SerializeField] private Transform furnitureContainer;
    [SerializeField] private FurniturePlacementValidator ghostValidator;
    [SerializeField] private LayerMask furnitureLayer;

    [SerializeField] private List<FurnitureInstance> _placedFurnitures = new List<FurnitureInstance>();

    [SerializeField] private Image circle;
    [SerializeField] private TMP_Text furnitureSlotName;

    [SerializeField] private Color greenSegment, redSegment, transparentSegment, greenOutline, redOutline;

    public Color GreenSegment => greenSegment;
    public Color RedSegment => redSegment;
    public Color TransparentSegment => transparentSegment;
    public Color GreenOutline => greenOutline;
    public Color RedOutline => redOutline;

    public int InventoryCount => _inventory.Count;
    public int CurrentInventoryIndex => _currentIndex;
    public bool HasFurnitureInInventory => _inventory.Count > 0;
    public bool IsBuildingMode { get; private set; }

    private const float GhostSpawnHeight = 0.25f;

    private Dictionary<FurnitureType, FurnitureData> _furnitureLibrary;
    private FurnitureData _currentSelected;
    private GameObject _activeGhost;

    private readonly List<InventoryItem> _inventory = new List<InventoryItem>();
    private int _currentIndex;

    private bool _hasShownBuildModeHint;
    private Tween _holdTween;
    private Tween _rotateTween;
    private Tween _cameraFovTween;
    private Sequence _spawnSequence;
    private float _baseCameraFov;
    private bool _hasBaseCameraFov;
    private readonly RaycastHit[] _pickupHitBuffer = new RaycastHit[16];

    private struct InventoryItem
    {
        public readonly FurnitureData Data;
        public readonly GameObject HeldInstance;
        public readonly Vector3 OriginalScale;

        public InventoryItem(FurnitureData data, GameObject heldInstance = null, Vector3 originalScale = default)
        {
            Data = data;
            HeldInstance = heldInstance;
            OriginalScale = originalScale;
        }
    }

    private void Awake()
    {
        ServiceLocator.Register(this);
        InitializeLibrary();
    }

    private void Start()
    {
        CacheBaseCameraFov();
    }

    private void OnDestroy()
    {
        KillGhostTweens();
        _holdTween?.Kill();
        _holdTween = null;
        _cameraFovTween?.Kill();
        _cameraFovTween = null;
    }

    private void InitializeLibrary()
    {
        _furnitureLibrary = new Dictionary<FurnitureType, FurnitureData>();
        foreach (FurnitureData data in availableFurniture)
        {
            if (data == null || _furnitureLibrary.ContainsKey(data.type)) continue;
            _furnitureLibrary.Add(data.type, data);
        }
    }

    private void Update()
    {
        UpdateInventoryDisplay();

        if (Input.GetKeyDown(KeyCode.B))
        {
            ToggleBuildingMode();
            return;
        }

        if (!IsBuildingMode) return;

        HandleBuildInput();
    }

    private void UpdateInventoryDisplay()
    {
        if (furnitureSlotName == null) return;

        if (!HasFurnitureInInventory)
        {
            furnitureSlotName.text = IsBuildingMode
                ? "Inventory: empty — hold RMB to pick up furniture"
                : "Inventory: empty";
            return;
        }

        int safeIndex = Mathf.Clamp(_currentIndex, 0, _inventory.Count - 1);
        string itemName = _inventory[safeIndex].Data.furnitureName;

        furnitureSlotName.text = _inventory.Count > 1
            ? $"Inventory: {itemName} ({safeIndex + 1}/{_inventory.Count}) — Q/E to navigate"
            : $"Inventory: {itemName} — press B";
    }

    public void ToggleBuildingMode()
    {
        if (!IsBuildingMode)
            TryEnterBuildMode();
        else
            ExitBuildMode();
    }

    private void TriggerWarning(string msg, bool isGood) =>
        ServiceLocator.Get<Warnings>().ShowWarning(msg, isGood);

    private void TryEnterBuildMode()
    {
        if (!ServiceLocator.Get<PlayerMotor>().PlayerIsInMarket)
        {
            TriggerWarning("The build mode only works inside the store!", false);
            return;
        }

        IsBuildingMode = true;

        if (HasFurnitureInInventory)
            LoadCurrentFromInventory();
    }

    private void ExitBuildMode()
    {
        IsBuildingMode = false;
        _currentSelected = null;
        CancelPickupHold();
        DismissGhostAnimated();
    }

    private void LoadCurrentFromInventory(Vector3? spawnPos = null, Quaternion? spawnRot = null)
    {
        if (_inventory.Count == 0)
        {
            DestroyGhostImmediate();
            _currentSelected = null;
            return;
        }

        _currentIndex = Mathf.Clamp(_currentIndex, 0, _inventory.Count - 1);
        _currentSelected = _inventory[_currentIndex].Data;

        Vector3 pos = spawnPos ?? GetDefaultGhostPosition();
        Quaternion rot = spawnRot ?? Quaternion.identity;

        SpawnGhost(pos, rot);
    }

    private void SpawnGhost(Vector3 position, Quaternion rotation)
    {
        DestroyGhostImmediate();

        if (_currentSelected == null || _currentSelected.ghostPrefab == null) return;

        _activeGhost = Instantiate(_currentSelected.ghostPrefab, position + Vector3.up * GhostSpawnHeight, rotation);
        _activeGhost.SetActive(true);
        ghostValidator = _activeGhost.GetComponent<FurniturePlacementValidator>();

        Transform ghostTransform = _activeGhost.transform;
        Vector3 originalScale = ghostTransform.localScale;
        ghostTransform.localScale = Vector3.zero;

        Collider[] ghostColliders = _activeGhost.GetComponentsInChildren<Collider>(true);
        SetCollidersEnabled(ghostColliders, false);

        _spawnSequence = DOTween.Sequence()
            .Append(ghostTransform.DOScale(originalScale * 1.2f, 0.15f).SetEase(Ease.OutCubic))
            .Append(ghostTransform.DOScale(originalScale * 0.88f, 0.1f).SetEase(Ease.OutQuad))
            .Append(ghostTransform.DOScale(originalScale * 1.08f, 0.07f).SetEase(Ease.OutQuad))
            .Append(ghostTransform.DOScale(originalScale, 0.05f).SetEase(Ease.OutQuad))
            .SetLink(_activeGhost)
            .OnComplete(() =>
            {
                _spawnSequence = null;
                SetCollidersEnabled(ghostColliders, true);
            });

        PlayCameraKick(5f, 0.1f, 0.22f, Ease.OutQuad);
    }

    private GameObject DetachGhost()
    {
        KillGhostTweens();

        GameObject ghost = _activeGhost;
        _activeGhost = null;

        FurniturePlacementValidator validator = ghostValidator;
        ghostValidator = null;

        if (ghost != null)
        {
            if (validator == null) validator = ghost.GetComponent<FurniturePlacementValidator>();
            if (validator != null) validator.Suspend();
        }

        return ghost;
    }

    private void DestroyGhostImmediate()
    {
        GameObject ghost = DetachGhost();
        if (ghost == null) return;

        ghost.transform.DOKill(false);
        Destroy(ghost);
    }

    private void DismissGhostAnimated()
    {
        GameObject ghost = DetachGhost();
        if (ghost == null) return;

        ghost.transform.DOKill(false);
        SetCollidersEnabled(ghost.GetComponentsInChildren<Collider>(true), false);

        ghost.transform.DOScale(Vector3.zero, 0.2f)
            .SetEase(Ease.InBack)
            .SetLink(ghost)
            .OnComplete(() => Destroy(ghost));
    }

    private void KillGhostTweens()
    {
        _rotateTween?.Kill();
        _rotateTween = null;
        _spawnSequence?.Kill();
        _spawnSequence = null;
    }

    private void SettleGhostTransform()
    {
        if (_activeGhost == null) return;

        _rotateTween?.Kill(true);
        _rotateTween = null;
        _activeGhost.transform.DOKill(true);
    }

    private static void SetCollidersEnabled(Collider[] colliders, bool enabled)
    {
        foreach (Collider col in colliders)
            if (col != null) col.enabled = enabled;
    }

    private Vector3 GetDefaultGhostPosition()
    {
        Camera cam = Camera.main;
        if (cam == null) return Vector3.zero;
        return cam.transform.position + cam.transform.forward * 4f;
    }

    public void AddToInventory(FurnitureData data)
    {
        _inventory.Add(new InventoryItem(data));

        if (!_hasShownBuildModeHint)
        {
            _hasShownBuildModeHint = true;
            ServiceLocator.Get<Warnings>().ShowWarning("Press B to enter build mode!", true);
        }
    }

    private void HandleBuildInput()
    {
        if (_activeGhost != null && Input.GetMouseButtonDown(0) && CanPlaceGhost())
            PlaceFurniture();

        if (!IsBuildingMode) return;

        if (_activeGhost != null && Input.GetKeyDown(KeyCode.R))
            RotateGhost();

        if (_inventory.Count > 1)
        {
            if (Input.GetKeyDown(KeyCode.Q)) CycleInventory(-1);
            if (Input.GetKeyDown(KeyCode.E)) CycleInventory(1);
        }

        HandlePickupHold();
    }

    private bool CanPlaceGhost()
    {
        return ghostValidator != null && ghostValidator.IsValid && _spawnSequence == null;
    }

    private void RotateGhost()
    {
        _rotateTween?.Kill(true);
        _rotateTween = null;
        _activeGhost.transform.DOKill(true);

        _rotateTween = _activeGhost.transform
            .DORotate(Vector3.up * 90f, 0.18f, RotateMode.LocalAxisAdd)
            .SetEase(Ease.OutBack)
            .SetLink(_activeGhost);
    }

    private void CycleInventory(int direction)
    {
        _currentIndex = (_currentIndex + direction + _inventory.Count) % _inventory.Count;

        if (_activeGhost != null)
        {
            SettleGhostTransform();
            _activeGhost.transform.GetPositionAndRotation(out Vector3 pos, out Quaternion rot);
            LoadCurrentFromInventory(pos, rot);
        }
        else
        {
            LoadCurrentFromInventory();
        }
    }

    private void HandlePickupHold()
    {
        if (circle == null) return;

        if (Input.GetMouseButtonDown(1))
            BeginPickupHold();

        if (Input.GetMouseButtonUp(1))
            CancelPickupHold();
    }

    private void BeginPickupHold()
    {
        if (!IsFurnitureUnderCrosshair()) return;

        _holdTween?.Kill();
        circle.DOKill(false);
        circle.fillAmount = 0f;

        _holdTween = circle.DOFillAmount(1f, 1f)
            .SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                _holdTween = null;
                TryPickUpFurniture();
                circle.DOFillAmount(0f, 0.2f).SetEase(Ease.OutQuad);
            });
    }

    private bool IsFurnitureUnderCrosshair()
    {
        Camera cam = Camera.main;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f));
        int hitCount = Physics.RaycastNonAlloc(ray, _pickupHitBuffer, 5f);

        for (int i = 0; i < hitCount; i++)
        {
            if (_pickupHitBuffer[i].collider.GetComponentInParent<FurnitureInstance>() != null)
                return true;
        }
        return false;
    }

    private void CancelPickupHold()
    {
        _holdTween?.Kill();
        _holdTween = null;

        if (circle == null) return;
        circle.DOKill(false);
        circle.DOFillAmount(0f, 0.2f).SetEase(Ease.OutQuad);
    }

    private void TryPickUpFurniture()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f));
        int hitCount = Physics.RaycastNonAlloc(ray, _pickupHitBuffer, 5f);

        FurnitureInstance instance = null;
        float closest = float.MaxValue;
        for (int i = 0; i < hitCount; i++)
        {
            var fi = _pickupHitBuffer[i].collider.GetComponentInParent<FurnitureInstance>();
            if (fi != null && _pickupHitBuffer[i].distance < closest)
            {
                instance = fi;
                closest = _pickupHitBuffer[i].distance;
            }
        }

        if (instance == null) return;

        if (instance.Data == null || !_furnitureLibrary.ContainsKey(instance.Data.type))
        {
            TriggerWarning("That furniture cannot be removed.", false);
            return;
        }

        _placedFurnitures.Remove(instance);
        _inventory.Add(new InventoryItem(instance.Data, instance.gameObject, instance.transform.localScale));

        AnimatePickedUpFurniture(instance.gameObject);

        if (_activeGhost == null)
        {
            _currentIndex = _inventory.Count - 1;
            LoadCurrentFromInventory();
        }

        ConstructionUI constructionUI = ServiceLocator.Get<ConstructionUI>();
        if (constructionUI != null) constructionUI.SetText();

        PlayCameraKick(6f, 0.06f, 0.2f, Ease.OutQuad);
    }

    private void AnimatePickedUpFurniture(GameObject target)
    {
        SetCollidersEnabled(target.GetComponentsInChildren<Collider>(true), false);

        Transform targetTransform = target.transform;
        targetTransform.DOKill(false);
        Vector3 originalScale = targetTransform.localScale;

        DOTween.Sequence()
            .Append(targetTransform.DOScale(originalScale * 1.2f, 0.08f).SetEase(Ease.OutQuad))
            .Append(targetTransform.DOScale(Vector3.zero, 0.25f).SetEase(Ease.InBack))
            .Join(targetTransform.DORotate(Vector3.up * 180f, 0.33f, RotateMode.LocalAxisAdd).SetEase(Ease.InQuad))
            .SetLink(target)
            .OnComplete(() => target.SetActive(false));
    }

    private void PlaceFurniture()
    {
        if (!ServiceLocator.Get<PlayerMotor>().PlayerIsInMarket)
        {
            TriggerWarning("You can only place furniture inside the store!", false);
            return;
        }

        if (_activeGhost == null || _inventory.Count == 0) return;

        _currentIndex = Mathf.Clamp(_currentIndex, 0, _inventory.Count - 1);
        InventoryItem inventoryItem = _inventory[_currentIndex];
        bool wasMovingExistingFurniture = inventoryItem.HeldInstance != null;
        FurnitureData selectedData = _currentSelected;

        SettleGhostTransform();
        _activeGhost.transform.GetPositionAndRotation(out Vector3 lastPos, out Quaternion lastRot);
        lastPos.y = selectedData.floorDistance;
        DestroyGhostImmediate();

        GameObject obj;
        Vector3 finalScale;

        if (wasMovingExistingFurniture)
        {
            obj = inventoryItem.HeldInstance;
            finalScale = inventoryItem.OriginalScale;

            obj.transform.DOKill(false);
            obj.transform.SetPositionAndRotation(lastPos, lastRot);
            obj.transform.SetParent(furnitureContainer);
            obj.SetActive(true);
        }
        else
        {
            obj = Instantiate(selectedData.prefab, lastPos, lastRot);
            finalScale = obj.transform.localScale;
            obj.transform.SetParent(furnitureContainer);
        }

        AnimatePlacedFurniture(obj.transform, finalScale);
        ServiceLocator.Get<SoundManager>().Play(SFX.FurnitureColocada);

        if (obj.TryGetComponent(out FurnitureInstance instance))
        {
            instance.Data = selectedData;
            _placedFurnitures.Add(instance);
        }

        _inventory.RemoveAt(_currentIndex);

        TutorialManager tutorialManager = ServiceLocator.Get<TutorialManager>();
        if (tutorialManager != null)
            tutorialManager.NotifyGameEvent(wasMovingExistingFurniture ? "HasMovedFurniture" : "HasPutFurniture");

        if (_inventory.Count > 0)
        {
            _currentIndex = Mathf.Clamp(_currentIndex, 0, _inventory.Count - 1);
            LoadCurrentFromInventory(lastPos, lastRot);
        }
        else
        {
            IsBuildingMode = false;
            _currentSelected = null;
            _currentIndex = 0;
            CancelPickupHold();
        }

        PlayCameraKick(-7f, 0.06f, 0.3f, Ease.OutElastic);
    }

    private void AnimatePlacedFurniture(Transform target, Vector3 finalScale)
    {
        target.localScale = Vector3.zero;

        Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
        SetCollidersEnabled(colliders, false);

        DOTween.Sequence()
            .Append(target.DOScale(finalScale * 1.3f, 0.12f).SetEase(Ease.OutCubic))
            .Append(target.DOScale(finalScale * 0.85f, 0.09f).SetEase(Ease.OutQuad))
            .Append(target.DOScale(finalScale * 1.08f, 0.07f).SetEase(Ease.OutQuad))
            .Append(target.DOScale(finalScale * 0.96f, 0.05f).SetEase(Ease.OutQuad))
            .Append(target.DOScale(finalScale, 0.04f).SetEase(Ease.OutQuad))
            .SetLink(target.gameObject)
            .OnComplete(() => SetCollidersEnabled(colliders, true));
    }

    private void CacheBaseCameraFov()
    {
        if (_hasBaseCameraFov) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        _baseCameraFov = cam.fieldOfView;
        _hasBaseCameraFov = true;
    }

    private void PlayCameraKick(float fovDelta, float inDuration, float returnDuration, Ease returnEase)
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        if (!_hasBaseCameraFov)
        {
            _baseCameraFov = cam.fieldOfView;
            _hasBaseCameraFov = true;
        }

        _cameraFovTween?.Kill(false);
        _cameraFovTween = DOTween.Sequence()
            .Append(cam.DOFieldOfView(_baseCameraFov + fovDelta, inDuration).SetEase(Ease.OutQuad))
            .Append(cam.DOFieldOfView(_baseCameraFov, returnDuration).SetEase(returnEase));
    }

    public void SelectFurniture(FurnitureType type)
    {
        if (!_furnitureLibrary.TryGetValue(type, out FurnitureData data))
        {
            Debug.LogError($"[FurnitureManager] Type '{type}' not found in the library.");
            return;
        }
        AddToInventory(data);
    }

    public List<FurnitureInstance> GetPlacedFurnitures() => _placedFurnitures;
    public FurnitureData GetCurrentSelected() => _currentSelected;
    public GameObject GetActiveGhost() => _activeGhost;
}
