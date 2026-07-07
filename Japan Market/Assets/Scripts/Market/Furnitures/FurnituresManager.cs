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

    private Dictionary<FurnitureType, FurnitureData> _furnitureLibrary;
    private FurnitureData _currentSelected;
    private GameObject _activeGhost;

    [SerializeField] private List<FurnitureInstance> _placedFurnitures = new List<FurnitureInstance>();

    private readonly List<InventoryItem> _inventory = new List<InventoryItem>();
    private int _currentIndex = 0;

    public int InventoryCount => _inventory.Count;
    public int CurrentInventoryIndex => _currentIndex;
    public bool HasFurnitureInInventory => _inventory.Count > 0;
    public bool IsBuildingMode { get; private set; }

    [SerializeField] private Image circle;
    [SerializeField] private TMP_Text furnitureSlotName;

    [SerializeField] private Color greenSegment, redSegment, transparentSegment, greenOutline, redOutline;
    public Color GreenSegment => greenSegment;
    public Color RedSegment => redSegment;
    public Color TransparentSegment => transparentSegment;
    public Color GreenOutline => greenOutline;
    public Color RedOutline => redOutline;

    private bool _hasShownBuildModeHint;
    private Tween _holdTween;

    private struct InventoryItem
    {
        public readonly FurnitureData Data;
        public readonly FurnitureSaveData SaveData;

        public InventoryItem(FurnitureData data, FurnitureSaveData saveData = null)
        {
            Data = data;
            SaveData = saveData;
        }
    }

    private void Awake()
    {
        ServiceLocator.Register(this);
        InitializeLibrary();
    }

    private void InitializeLibrary()
    {
        _furnitureLibrary = new Dictionary<FurnitureType, FurnitureData>();
        foreach (var data in availableFurniture)
            _furnitureLibrary.Add(data.type, data);
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
        if (!HasFurnitureInInventory)
        {
            furnitureSlotName.text = IsBuildingMode
                ? "Inventário: vazio — segure RMB para pegar furniture"
                : "Inventário: vazio";
            return;
        }

        int safeIndex = Mathf.Clamp(_currentIndex, 0, _inventory.Count - 1);
        string itemName = _inventory[safeIndex].Data.furnitureName;

        furnitureSlotName.text = _inventory.Count > 1
            ? $"Inventário: {itemName} ({safeIndex + 1}/{_inventory.Count}) — Q/E para navegar"
            : $"Inventário: {itemName} — pressione B";
    }

    // ─── Build Mode ───────────────────────────────────────────────────────────

    public void ToggleBuildingMode()
    {
        if (!IsBuildingMode)
            TryEnterBuildMode();
        else
            ExitBuildMode();
    }

    private void TryEnterBuildMode()
    {
        if (!ServiceLocator.Get<PlayerMotor>().PlayerIsInMarket)
        {
            ServiceLocator.Get<Warnings>().ShowWarning("O modo de construção só funciona dentro da loja!", false);
            return;
        }

        IsBuildingMode = true;

        // Mesmo sem inventário entra no modo — permite pegar furnitures colocadas
        if (HasFurnitureInInventory)
            LoadCurrentFromInventory();
    }

    private void ExitBuildMode()
    {
        IsBuildingMode = false;
        if (_activeGhost != null)
        {
            Destroy(_activeGhost);
            _activeGhost = null;
        }
        _currentSelected = null;
    }

    private void LoadCurrentFromInventory(Vector3? spawnPos = null, Quaternion? spawnRot = null)
    {
        if (_inventory.Count == 0)
        {
            if (_activeGhost != null) { Destroy(_activeGhost); _activeGhost = null; }
            _currentSelected = null;
            return;
        }

        _currentIndex = Mathf.Clamp(_currentIndex, 0, _inventory.Count - 1);
        _currentSelected = _inventory[_currentIndex].Data;

        Vector3 pos = spawnPos ?? GetDefaultGhostPosition();
        Quaternion rot = spawnRot ?? Quaternion.identity;

        if (_activeGhost != null) Destroy(_activeGhost);
        _activeGhost = Instantiate(_currentSelected.ghostPrefab, pos, rot);
        _activeGhost.SetActive(true);
        ghostValidator = _activeGhost.GetComponent<FurniturePlacementValidator>();
    }

    private Vector3 GetDefaultGhostPosition()
    {
        Camera cam = Camera.main;
        if (cam == null) return Vector3.zero;
        return cam.transform.position + cam.transform.forward * 4f;
    }

    // ─── Inventário ───────────────────────────────────────────────────────────

    public void AddToInventory(FurnitureData data)
    {
        _inventory.Add(new InventoryItem(data));

        if (!_hasShownBuildModeHint)
        {
            _hasShownBuildModeHint = true;
            ServiceLocator.Get<Warnings>().ShowWarning("Pressione B para entrar no modo de construção!", true);
        }
    }

    // ─── Input do Build Mode ──────────────────────────────────────────────────

    private void HandleBuildInput()
    {
        if (_activeGhost != null)
        {
            bool canPlace = ghostValidator != null && ghostValidator.IsValid;
            if (Input.GetMouseButtonDown(0) && canPlace)
                PlaceFurniture();

            if (Input.GetKeyDown(KeyCode.R))
                _activeGhost.transform.Rotate(0, 90, 0);
        }

        // Q/E navegam pelo inventário quando há mais de 1 item
        if (_inventory.Count > 1)
        {
            if (Input.GetKeyDown(KeyCode.Q)) CycleInventory(-1);
            if (Input.GetKeyDown(KeyCode.E)) CycleInventory(1);
        }

        HandlePickupHold();
    }

    private void CycleInventory(int direction)
    {
        _currentIndex = (_currentIndex + direction + _inventory.Count) % _inventory.Count;

        if (_activeGhost != null)
        {
            var t = _activeGhost.transform;
            LoadCurrentFromInventory(t.position, t.rotation);
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
        {
            _holdTween = circle.DOFillAmount(1f, 1f).OnComplete(() =>
            {
                TryPickUpFurniture();
                circle.DOFillAmount(0f, 0.2f);
            });
        }

        if (Input.GetMouseButtonUp(1))
        {
            _holdTween?.Kill();
            circle.DOFillAmount(0f, 0.2f);
        }
    }

    // ─── Pegar Furniture Colocada ─────────────────────────────────────────────

    private void TryPickUpFurniture()
    {
        Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f));
        if (!Physics.Raycast(ray, out RaycastHit hit, 5f, furnitureLayer)) return;

        FurnitureInstance instance = hit.collider.GetComponentInParent<FurnitureInstance>();
        if (instance == null) return;

        if (!_furnitureLibrary.ContainsKey(instance.Data.type))
        {
            ServiceLocator.Get<Warnings>().ShowWarning("Esta furniture não pode ser removida.", false);
            return;
        }

        _placedFurnitures.Remove(instance);
        _inventory.Add(new InventoryItem(instance.Data, instance.SaveData));
        Destroy(instance.gameObject);

        // Se inventário estava vazio (sem ghost), carrega o item recém-pego
        if (_activeGhost == null)
        {
            _currentIndex = _inventory.Count - 1;
            LoadCurrentFromInventory();
        }

        ServiceLocator.Get<ConstructionUI>().SetText();
    }

    // ─── Colocar Furniture ────────────────────────────────────────────────────

    private void PlaceFurniture()
    {
        if (!ServiceLocator.Get<PlayerMotor>().PlayerIsInMarket)
        {
            ServiceLocator.Get<Warnings>().ShowWarning("Só é possível colocar furniture dentro da loja!", false);
            return;
        }

        var inventoryItem = _inventory[_currentIndex];
        bool wasMovingExistingFurniture = inventoryItem.SaveData != null;

        // Salva posição/rotação do ghost antes de destruí-lo
        _activeGhost.transform.GetPositionAndRotation(out Vector3 lastPos, out Quaternion lastRot);

        GameObject obj = Instantiate(_currentSelected.prefab, lastPos, lastRot);
        obj.transform.SetParent(furnitureContainer);

        // Animação satisfatória: punch na escala correta do prefab (sem risco de escala errada)
        obj.transform.DOPunchScale(Vector3.one * 0.2f, 0.4f, 5, 0.5f);

        if (obj.TryGetComponent(out FurnitureInstance instance))
        {
            instance.Data = _currentSelected;
            if (inventoryItem.SaveData != null)
                instance.SaveData = inventoryItem.SaveData;
            _placedFurnitures.Add(instance);
        }

        _inventory.RemoveAt(_currentIndex);
        Destroy(_activeGhost);
        _activeGhost = null;
        _currentSelected = null;

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
            _currentIndex = 0;
            // Mantém build mode ativo para poder pegar furnitures colocadas
        }
    }

    // ─── API pública ──────────────────────────────────────────────────────────

    public void SelectFurniture(FurnitureType type)
    {
        if (!_furnitureLibrary.TryGetValue(type, out FurnitureData data))
        {
            Debug.LogError($"[FurnitureManager] Tipo '{type}' não encontrado na library.");
            return;
        }
        AddToInventory(data);
    }

    public List<FurnitureInstance> GetPlacedFurnitures() => _placedFurnitures;
    public FurnitureData GetCurrentSelected() => _currentSelected;
    public GameObject GetActiveGhost() => _activeGhost;
}
