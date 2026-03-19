using UnityEngine;
using System.Collections.Generic;
using TMPro;
using DG.Tweening;
using Unity.Cinemachine;
public class CashRegister : MonoBehaviour
{
    [SerializeField] List<AllIThingsData> allItem;
    [SerializeField] PlayerMotor playerMotor;
    [SerializeField] PlayerLook playerLook;
    [SerializeField] PaymentMoney paymentMoney;
    [SerializeField] PaymentCard paymentCard;
    [SerializeField] TextMeshPro nameItemText;
    [SerializeField] TextMeshPro priceItemText;
    [SerializeField] TextMeshPro totalPriceText;
    [SerializeField] Transform bagPoint;
    [SerializeField] Transform bagTopPoint;
    [SerializeField] GameObject creditCard;
    [SerializeField] GameObject money;
    [SerializeField] GameObject quitButton;
    [SerializeField] Transform cashPosition;
    [SerializeField] CinemachineCamera cam;
    [SerializeField] float zoom = 25f;
    float zoomOri;
    Queue<Item> itemsQueue = new Queue<Item>();
    float totalPrice = 0f;
    bool playerInRange = false;
    bool cashMode = false;
    public Transform[] queuePoints;
    private List<NpcTraject> queue = new List<NpcTraject>();

    void Awake()
    {
        ServiceLocator.Register(this);
    }
    public void EnterQueue(NpcTraject npc)
    {
        queue.Add(npc);
        UpdateQueue();
    }

    public void LeaveQueue(NpcTraject npc)
    {
        queue.Remove(npc);
        UpdateQueue();
    }

    void UpdateQueue()
    {
        for (int i = 0; i < queue.Count; i++)
        {
            queue[i].GetComponent<NpcTraject>().SetTarget(queuePoints[i], i);
        }
    }
    public NpcTraject GetCurrentCustomer()
    {
        if (queue.Count > 0)
            return queue[0];

        return null;
    }
    void Start()
    {
        creditCard.SetActive(false);
        money.SetActive(false);
        quitButton.SetActive(false);

        totalPriceText.text = "";
        nameItemText.text = "";
        priceItemText.text = "";

        zoomOri = cam.Lens.FieldOfView;
    }
    void Update()
    {
        if (playerInRange && !cashMode && Input.GetButtonDown("Fire1"))
        {
            EnterCashMode();
            print("Enter Cash Mode");
        }

        if (cashMode && Input.GetButtonDown("Fire1"))
        {
            ItemClicked();
        }
    }
    void EnterCashMode()
    {
        print("Enter Cash Mode");
        cashMode = true;
        cam.Priority = 6;

        playerLook.ResetLook();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        quitButton.SetActive(true);
        playerMotor.SetCanMove(false);
        playerLook.CanLook = false;

        Transform player = playerMotor.transform;

        Sequence seq = DOTween.Sequence();

        seq.Append(player.DOMove(cashPosition.position, 0.3f)
            .SetEase(Ease.OutQuad));

        seq.Join(DOTween.To(
            () => cam.Lens.FieldOfView,
            x => cam.Lens.FieldOfView = x,
            zoom,
            0.4f).SetEase(Ease.OutQuad));
    }
    public void ExitCashMode()
    {
        cashMode = false;
        cam.Priority = 0;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        quitButton.SetActive(false);
        playerMotor.SetCanMove(true);
        playerLook.CanLook = true;

        DOTween.To(
            () => cam.Lens.FieldOfView,
            x => cam.Lens.FieldOfView = x,
            zoomOri,
            0.35f).SetEase(Ease.OutQuad);

        totalPrice = 0;

        if (paymentMoney != null && paymentCard != null)
        {
            paymentMoney.ClosePaymentMoney();
            paymentCard.ClosePaymentCredi();
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
            return;
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

        if (item.TryGetComponent(out Collider col))
            col.enabled = false;

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
                    Invoke(nameof(BuyTotal), 0.34f);
                    creditCard.SetActive(true);
                    money.SetActive(true);
                }

                break;
            }
        }
    }
    void BuyTotal()
    {
        nameItemText.text = "";
        priceItemText.text = "";
        totalPriceText.text = "Total ¥" + totalPrice.ToString("F2");
    }
    public float GetTotalPrice()
    {
        return totalPrice;
    }
    public void FinishPayment()
    {
        creditCard.SetActive(false);
        money.SetActive(false);
        totalPriceText.text = "";
        totalPrice = 0;
    }
    [ContextMenu("Finish Customer")]
    public void FinishCustomer()
    {
        if (queue.Count == 0) return;

        var npc = queue[0];

        LeaveQueue(npc);

      //  npc.GoAway();
    }
}