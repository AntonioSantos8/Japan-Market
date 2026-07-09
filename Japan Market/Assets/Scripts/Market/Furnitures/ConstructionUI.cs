using DG.Tweening;
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
    private bool _wasBuilding;

    private void Start()
    {
        ServiceLocator.Register(this);
        _manager = ServiceLocator.Get<FurnitureManager>();
        panelMode.SetActive(false);
    }

    private void Update()
    {
        bool isBuilding = _manager.IsBuildingMode;

        if (isBuilding != _wasBuilding)
        {
            _wasBuilding = isBuilding;
            if (isBuilding)
            {
                panelMode.SetActive(true);
                panelMode.transform.localScale = Vector3.zero;
                panelMode.transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack);
            }
            else
            {
                panelMode.transform.DOKill();
                panelMode.transform.DOScale(Vector3.zero, 0.2f)
                    .SetEase(Ease.InBack)
                    .OnComplete(() => panelMode.SetActive(false));
            }
        }

        if (isBuilding && _manager.HasFurnitureInInventory)
            SetText();
        else
            textCurrentItem.text = "Nenhum selecionado";
    }

    public void SetText()
    {
        var current = _manager.GetCurrentSelected();
        if (current == null)
        {
            textCurrentItem.text = "Nenhum selecionado";
            if (statusIndicator != null) statusIndicator.color = inactiveColor;
            return;
        }

        int count = _manager.InventoryCount;
        textCurrentItem.text = count > 1
            ? $"Item: {current.furnitureName} ({count} restantes)"
            : $"Item: {current.furnitureName}";

        if (statusIndicator != null) statusIndicator.color = activeColor;
    }
}
