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
        ServiceLocator.Register(this);
        _manager = ServiceLocator.Get<FurnitureManager>();
    }

    private void Update()
    {
        bool isBuilding = _manager.IsBuildingMode;
        panelMode.SetActive(isBuilding);
        if (isBuilding && _manager.hasFurnitureInInventory)
        {
           SetText();
        }
        else
        {
            textCurrentItem.text = "Nenhum selecionado";
        }
    }
    public void SetText()
    {
        var current = _manager.GetCurrentSelected();
        textCurrentItem.text = "Item: " + current.furnitureName;
    }
}