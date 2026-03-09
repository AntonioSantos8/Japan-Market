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

        if (item.TryGetComponent(out Collider col)) col.enabled = false;

        if (item.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        Sequence seq = DOTween.Sequence();

      
        seq.Append(item.transform.DOMoveY(item.transform.position.y + 0.3f, 0.15f)
            .SetEase(Ease.OutQuad));

       
        seq.Append(item.transform.DOMove(bagTopPoint.position, 0.25f)
            .SetEase(Ease.InOutQuad));

        
        seq.Append(item.transform.DOMove(bagPoint.position, 0.2f)
            .SetEase(Ease.InQuad));

       
        seq.Append(item.transform.DOPunchScale(Vector3.one * 0.15f, 0.2f, 5));

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
                    Invoke(nameof(ShowTotal), 0.5f);
                }

                break;
            }
        }
    }
    void ShowTotal()
    {
        nameItemText.text = "";
        priceItemText.text = "";
        totalPriceText.text = "Total ¥" + totalPrice.ToString("F2");
    }
}