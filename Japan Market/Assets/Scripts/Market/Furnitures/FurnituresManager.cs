using System.Collections.Generic;
using UnityEngine;

public class FurnitureManager : MonoBehaviour
{
    [SerializeField] private List<FurnitureData> availableFurniture;
    [SerializeField] private Transform furnitureContainer;
    [SerializeField] private FurniturePlacementValidator ghostValidator;

    private Dictionary<FurnitureType, FurnitureData> _furnitureLibrary;
    private FurnitureData _currentSelected;
    private GameObject _activeGhost;

    private List<FurnitureInstance> _placedFurnitures = new List<FurnitureInstance>();
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
        HandleInput();
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) SelectFurniture(FurnitureType.Shelf);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SelectFurniture(FurnitureType.Freezer);

        if (_activeGhost != null)
        {
            if (Input.GetKeyDown(KeyCode.E) && ghostValidator.IsValid)
            {
                PlaceFurniture();
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                _activeGhost.transform.Rotate(0, 90, 0);
            }
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

    private void PlaceFurniture()
    {
        GameObject obj = Instantiate(_currentSelected.prefab, _activeGhost.transform.position, _activeGhost.transform.rotation);
        obj.transform.SetParent(furnitureContainer);

        if (obj.TryGetComponent(out FurnitureInstance instance))
        {
            _placedFurnitures.Add(instance);
        }
    }

    public List<FurnitureInstance> GetPlacedFurnitures() => _placedFurnitures;

    public GameObject GetActiveGhost() => _activeGhost;
}