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

    private FurnitureSaveData _tempSaveData;
    public bool IsBuildingMode { get; private set; }

    [SerializeField] private Image circle;
    public bool HasFurnitureInInventory;

    [SerializeField] private TMP_Text furnitureSlotName;


    [SerializeField] Color greenSegment, redSegment, transparentSegment, greenOutline, redOutline;
    public Color GreenSegment => greenSegment;
    public Color RedSegment => redSegment;
    public Color TransparentSegment => transparentSegment;
    public Color GreenOutline => greenOutline;
    public Color RedOutline => redOutline;





    private void Awake()
    {
        ServiceLocator.Register(this);
        InitializeLibrary();
    }

    private void InitializeLibrary()
    {
        _furnitureLibrary = new Dictionary<FurnitureType, FurnitureData>();
        foreach (var data in availableFurniture)
        {
            _furnitureLibrary.Add(data.type, data);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            ToggleBuildingMode();
            return;
        }
        if (HasFurnitureInInventory)
            furnitureSlotName.text = $"Inventory: {_currentSelected.name} (press B)";
        else
            furnitureSlotName.text = "Inventory: none";
        if (!IsBuildingMode) return;

        HandleInput();
    }

    public void ToggleBuildingMode()
    {
        IsBuildingMode = !IsBuildingMode;
        _activeGhost?.SetActive(IsBuildingMode);
    }
    private void HandleInput()
    {

        if (_activeGhost != null)
        {
            if (Input.GetMouseButtonDown(0) && ghostValidator.IsValid)
            {
                PlaceFurniture();
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                _activeGhost.transform.Rotate(0, 90, 0);
            }
        }
        else
        {
            VerifyMouseHold();
        }
    }
    Tween holdTween;

    private void VerifyMouseHold()
    {
        if (HasFurnitureInInventory == true)
        {
            print("has furniture in inventory");
            ServiceLocator.Get<Warnings>().ShowWarning("Your inventory is full.", false);
            return;
        }

        if (Input.GetMouseButtonDown(1))
        {
            print("holding");
            holdTween = circle.DOFillAmount(1f, 1f)
                .OnComplete(() =>
                {
                    TryPickUpFurniture();
                    circle.DOFillAmount(0f, 0.2f);
                });
        }

        if (Input.GetMouseButtonUp(1))
        {
            print("stop holding");
            holdTween?.Kill();
            circle.DOFillAmount(0f, 0.2f);
        }
    }
    public void SelectFurniture(FurnitureType type)
    {
        if (!_furnitureLibrary.TryGetValue(type, out FurnitureData data))
        {
            Debug.LogError($"[FurnitureManager] Tipo '{type}' não encontrado na library. Verifique se o FurnitureData está na lista 'availableFurniture'.");
            return;
        }

        if (_activeGhost != null) Destroy(_activeGhost);

        _currentSelected = data;
        _activeGhost = Instantiate(_currentSelected.ghostPrefab);
        ghostValidator = _activeGhost.GetComponent<FurniturePlacementValidator>();
    }

    private void TryPickUpFurniture()
    {
        if (HasFurnitureInInventory == true)
        {
            print("has furniture in inventory");
            ServiceLocator.Get<Warnings>().ShowWarning("Your inventory is full.", false);
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2));

        if (Physics.Raycast(ray, out RaycastHit hit, 5f, furnitureLayer))
        {
            FurnitureInstance instance = hit.collider.GetComponentInParent<FurnitureInstance>();
            if (instance == null) return;

            if (!_furnitureLibrary.ContainsKey(instance.Data.type))
            {
                Debug.LogError($"[FurnitureManager] '{instance.Data.type}' não está na library. Pickup cancelado.");
                ServiceLocator.Get<Warnings>().ShowWarning("This furniture can't be picked up right now.", false);
                return;
            }

            _placedFurnitures.Remove(instance);
            HasFurnitureInInventory = true;
            _tempSaveData = instance.SaveData;
            SelectFurniture(instance.Data.type);
            _activeGhost.transform.rotation = instance.transform.rotation;
            Destroy(instance.gameObject);
            ServiceLocator.Get<ConstructionUI>().SetText();
        }
    }

    private void PlaceFurniture()
    {
        if (ServiceLocator.Get<PlayerMotor>().PlayerIsInMarket == false)
        {
            ServiceLocator.Get<Warnings>().ShowWarning("You can only place furniture inside the market.", false);
            return;
        }

        GameObject obj = Instantiate(_currentSelected.prefab, _activeGhost.transform.position, _activeGhost.transform.rotation);
        obj.transform.SetParent(furnitureContainer);

        if (obj.TryGetComponent(out FurnitureInstance instance))
        {
            instance.Data = _currentSelected;

            if (_tempSaveData != null)
            {
                instance.SaveData = _tempSaveData;
                _tempSaveData = null;
            }

            _placedFurnitures.Add(instance);
        }

        _currentSelected = null;
        HasFurnitureInInventory = false;
        Destroy(_activeGhost);
        _activeGhost = null;

        ToggleBuildingMode();
    }

    public List<FurnitureInstance> GetPlacedFurnitures() => _placedFurnitures;
    public FurnitureData GetCurrentSelected() => _currentSelected;
    public GameObject GetActiveGhost() => _activeGhost;
}