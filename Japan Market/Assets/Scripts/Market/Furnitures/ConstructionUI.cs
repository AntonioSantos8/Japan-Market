using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ConstructionUI : MonoBehaviour
{
    [SerializeField] private GameObject panelMode;
    [SerializeField] private TextMeshProUGUI textCurrentItem;
    [SerializeField] private Color activeColor = Color.green;
    [SerializeField] private Color inactiveColor = Color.red;
    [SerializeField] private Image statusIndicator;

    private FurnitureManager _manager;

    private void Start()
    {
        _manager = ServiceLocator.Get<FurnitureManager>();
    }

    private void Update()
    {
        bool isBuilding = _manager.IsBuildingMode;
        panelMode.SetActive(isBuilding);
        if (isBuilding && _manager.hasFurnitureInInventory)
        {
            var current = _manager.GetCurrentSelected();
            textCurrentItem.text = "Item: " + current.furnitureName;
        }
        else
        {
            textCurrentItem.text = "Nenhum selecionado";
        }
    }
}