using System.Collections.Generic;
using UnityEngine;

public class FurnitureManager : MonoBehaviour
{
    [SerializeField] private List<FurnitureData> availableFurniture;
    [SerializeField] private Transform furnitureContainer;
    [SerializeField] private FurniturePlacementValidator ghostValidator;
    [SerializeField] private LayerMask furnitureLayer;

    private Dictionary<FurnitureType, FurnitureData> _furnitureLibrary;
    private FurnitureData _currentSelected;
    private GameObject _activeGhost;

    private List<FurnitureInstance> _placedFurnitures = new List<FurnitureInstance>();

    private FurnitureSaveData _tempSaveData;
    public bool IsBuildingMode { get; private set; }

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
        if (Input.GetKeyDown(KeyCode.B)) ToggleBuildingMode();
        if (!IsBuildingMode) return;

        HandleInput();
    }

    private void ToggleBuildingMode()
    {
        IsBuildingMode = !IsBuildingMode;
        if (!IsBuildingMode && _activeGhost != null) Destroy(_activeGhost);
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) SelectFurniture(FurnitureType.Shelf);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SelectFurniture(FurnitureType.Freezer);

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
            if (Input.GetMouseButtonDown(1)) TryPickUpFurniture();
        }
    }

    public void SelectFurniture(FurnitureType type)
    {
        if (_activeGhost != null) Destroy(_activeGhost);

        if (_furnitureLibrary.TryGetValue(type, out _currentSelected))
        {
            _activeGhost = Instantiate(_currentSelected.ghostPrefab);
            ghostValidator = _activeGhost.GetComponent<FurniturePlacementValidator>();
        }
    }

    private void TryPickUpFurniture()
    {
        Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2));
        if (Physics.Raycast(ray, out RaycastHit hit, 5f, furnitureLayer))
        {
            if (hit.collider.TryGetComponent(out FurnitureInstance instance))
            {
                _placedFurnitures.Remove(instance);

                _tempSaveData = instance.SaveData;

                SelectFurniture(instance.Data.type);
                _activeGhost.transform.rotation = instance.transform.rotation;
                Destroy(instance.gameObject);
            }
        }
    }

    private void PlaceFurniture()
    {
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

        Destroy(_activeGhost);
        _activeGhost = null;
    }

    public List<FurnitureInstance> GetPlacedFurnitures() => _placedFurnitures;
    public FurnitureData GetCurrentSelected() => _currentSelected;
    public GameObject GetActiveGhost() => _activeGhost;
}