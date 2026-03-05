using UnityEngine;
using System.Collections.Generic;
using TMPro;
using DG.Tweening;
public class CashRegister : MonoBehaviour
{
    [SerializeField] private List<AllIThingsData> allItem;
    [SerializeField] private TextMeshPro nameItemText;
    [SerializeField] private TextMeshPro priceItemText;
    [SerializeField] private TextMeshPro totalPriceText;
    [SerializeField] private Transform conveyorScanerPoint;
    [SerializeField] private Transform conveyorEndPoint;
    [SerializeField] private float waitTime = 2f;
    [SerializeField] Items[] itemsCustomers;
    private Item currentItem;
    private float totalPrice = 0;
    
    void Update()
    {
        if (Input.GetButtonDown("Fire1") && currentItem != null)
        {
            SendItemToConveyor(currentItem);
            currentItem = null;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        Item item = other.GetComponent<Item>();

        if (item != null)
        {
            currentItem = item;
        }
    }

    void SendItemToConveyor(Item item)
    {
        Sequence seq = DOTween.Sequence();

        seq.Append(item.transform.DOMove(conveyorScanerPoint.position, 1f));

        seq.AppendCallback(() =>
        {
            ProcessItem(item);
        });

        seq.AppendInterval(waitTime);

        seq.Append(item.transform.DOMove(conveyorEndPoint.position, 1f));
    }

    void ProcessItem(Item item)
    {
        Items type = item.GetItemType();

        foreach (var data in allItem)
        {
            if (data.itemType == type)
            {
                nameItemText.text = data.itemName;
                priceItemText.text = "¥" + data.singleItemPrice.ToString("F2");

                totalPrice += data.singleItemPrice;
                totalPriceText.text = "Total ¥" + totalPrice.ToString("F2");

                break;
            }
        }
    }
}