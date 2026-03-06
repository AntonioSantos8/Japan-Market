using UnityEngine;
using System.Collections.Generic;
using TMPro;
using DG.Tweening;
public class CashRegister : MonoBehaviour
{
    [SerializeField] List<AllIThingsData> allItem;
    [SerializeField] TextMeshPro nameItemText;
    [SerializeField] TextMeshPro priceItemText;
    [SerializeField] TextMeshPro totalPriceText;
    [SerializeField] Transform bagPoint;
    [SerializeField] Transform bagTopPoint;
    Queue<Item> itemsQueue = new Queue<Item>();
    float totalPrice = 0f;
    bool playerInRange = false;

    void Start()
    {
        totalPriceText.text = "";
    }

    void Update()
    {
        if (!playerInRange) return;

        if (Input.GetButtonDown("Fire1"))
        {
            TryProcessClickedItem();
        }
    }

    void TryProcessClickedItem()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            Item item = hit.collider.GetComponent<Item>();

            if (item != null && !item.PassedItem())
            {
                if (itemsQueue.Contains(item))
                {
                    RemoveFromQueue(item);
                    SendItemToBag(item);
                }
            }
        }
    }
    void RemoveFromQueue(Item item)
    {
        Queue<Item> newQueue = new Queue<Item>();

        foreach (var i in itemsQueue)
        {
            if (i != item)
                newQueue.Enqueue(i);
        }

        itemsQueue = newQueue;
    }
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
        }

        Item item = other.GetComponent<Item>();

        if (item != null && !item.PassedItem())
        {
            itemsQueue.Enqueue(item);
        }
    }
    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
        }
    }

    void SendItemToBag(Item item)
    {
        item.MarkAsPast();

        Sequence seq = DOTween.Sequence();

        seq.Append(item.transform.DOMove(bagTopPoint.position, 0.3f));
        seq.Append(item.transform.DOMove(bagPoint.position, 0.4f));

        seq.AppendCallback(() =>
        {
            PastItem(item);
        });
    }

    void PastItem(Item item)
    {
        Items type = item.GetItemType();

        foreach (var data in allItem)
        {
            if (data.itemType == type)
            {
                nameItemText.text = data.itemName;
                priceItemText.text = "¥" + data.singleItemPrice.ToString("F2");

                totalPrice += data.singleItemPrice;

                if (itemsQueue.Count == 0)
                {
                    nameItemText.text = "";
                    priceItemText.text = "";
                    totalPriceText.text = "Total ¥" + totalPrice.ToString("F2");
                }

                break;
            }
        }
    }
}
