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
    [SerializeField] GameObject creditCard;
    Queue<Item> itemsQueue = new Queue<Item>();
    float totalPrice = 0f;
    bool playerInRange = false;


    void Start()
    {
        creditCard.SetActive(false);
        totalPriceText.text = "";
    }

    void Update()
    {
        if (!playerInRange) return;

        if (Input.GetButtonDown("Fire1"))
        {
            ItemClicked();
        }
    }

    void ItemClicked()
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
                    RemoveQueue(item);
                    SendItemToBag(item);
                }
            }
        }
    }
    void RemoveQueue(Item item)
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

      
        seq.Append(item.transform.DOMoveY(item.transform.position.y + 0.32f, 0.12f)
            .SetEase(Ease.OutQuad));

       
        seq.Append(item.transform.DOMove(bagTopPoint.position, 0.18f)
            .SetEase(Ease.InOutQuad));

        
        seq.Append(item.transform.DOMove(bagPoint.position, 0.19f)
            .SetEase(Ease.InQuad));

       
        seq.Append(item.transform.DOPunchScale(Vector3.one * 0.12f, 0.26f, 5));

        seq.AppendCallback(() =>
        {
            item.transform.SetParent(bagPoint);
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