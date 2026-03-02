using UnityEngine;
using System.Collections.Generic;
using TMPro;
public class CashRegister : MonoBehaviour
{
    [SerializeField] private Collider triggerCol;
    [SerializeField] private List<AllIThingsData> allItem;
    [SerializeField] private TextMeshPro nameItemText;
    [SerializeField] private TextMeshPro priceItemText;

    void OnTriggerEnter(Collider other)
    {
      if (other == triggerCol) return;

        Item item = other.GetComponent<Item>();

        if (item != null)
        {
            Items type = item.GetItemType();

            
            foreach (var data in allItem)
            {
                if (data.itemType == type)
                {
                    ShowItemInfo(data);
                    break; 
                }
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        Item item = other.GetComponent<Item>();

        if (item != null)
        {
            ClearItem();
        }
    }

    void ShowItemInfo(AllIThingsData data)
    {
        nameItemText.text = data.itemName;
        priceItemText.text = "R$ " + data.itemPrice.ToString("F2");
    }

    void ClearItem()
    {
        nameItemText.text = "";
        priceItemText.text = "";
    }
}