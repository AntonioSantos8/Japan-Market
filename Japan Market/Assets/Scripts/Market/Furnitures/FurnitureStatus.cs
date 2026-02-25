using TMPro;
using UnityEngine;

public class FurnitureStatus : MonoBehaviour
{
    private FurnitureManager _manager;
    [SerializeField] private FurnitureType _type;
    private void Start()
    {
        if (_manager == null) return;
        _manager = ServiceLocator.Get<FurnitureManager>();
        _type = _manager.GetCurrentSelected().type;
    }
}
