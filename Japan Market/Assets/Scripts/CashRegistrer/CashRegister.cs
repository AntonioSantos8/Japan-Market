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
    [SerializeField] TextMeshPro cashregisterText;
    [SerializeField] Transform bagPoint;
    [SerializeField] Transform bagTopPoint;
    [SerializeField] GameObject creditCard;
    [SerializeField] GameObject money;
    [SerializeField] GameObject quitButton;
    [SerializeField] Transform cashPosition;
    [SerializeField] CinemachineCamera cam;
    [SerializeField] GameObject reticle;
    public Transform itemPosition;
    [SerializeField] float zoom = 25f;
    float zoomOri;
    Queue<Item> itemsQueue = new Queue<Item>();
    float totalPrice = 0f;
    bool playerInRange = false;
    bool cashMode = false;
    public Transform[] queuePoints;
    private List<NpcTraject> queue = new List<NpcTraject>();
    public bool hasClient;

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
    public GameObject GetItemPrefab(Items type)
    {
        foreach (var data in allItem)
        {
            if (data.itemType == type)
            {
                return data.itemPrefab;
            }
        }
        return null;
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
        cashregisterText.gameObject.SetActive(false);

        cashregisterText.text = "";
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
        }
        if (cashMode && Input.GetButtonDown("Fire1"))
        {
            ItemClicked();
        }
    }
    void EnterCashMode()
    {
        cashMode = true;


        playerLook.ResetLook();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        quitButton.SetActive(true);
        playerMotor.SetCanMove(false);
        playerMotor.ResetCameraEffects();
        playerLook.CanLook = false;
        reticle.SetActive(false);

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


        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        quitButton.SetActive(false);
        playerMotor.SetCanMove(true);
        playerLook.CanLook = true;
        reticle.SetActive(true);

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

        seq.Append(item.transform.DOPunchScale(Vector3.one * 0.18f, 0.15f, 6, 1));

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
        seq.AppendCallback(() =>
        {
            Destroy(item.gameObject);
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
                float price = item.gameObject.GetComponent<ItemPrice>().Price;
                // priceItemText.text = "¥" + data.singleItemPrice.ToString("F2");
                // totalPrice += data.singleItemPrice;
                priceItemText.text = "¥" + price.ToString("F2");
                totalPrice += price;

                if (itemsQueue.Count == 0)
                {
                    Invoke(nameof(BuyTotal), 0.34f);

                    creditCard.SetActive(false);
                    money.SetActive(false);

                    var customer = GetCurrentCustomer();

                    if (customer != null)
                    {
                        var instance = customer.GetComponent<NpcInstance>();

                        if (instance.paymentType == PaymentType.Card)
                        {
                            creditCard.SetActive(true);
                        }
                        else
                        {
                            money.SetActive(true);
                        }
                    }
                }

                break;
            }
        }
    }
    public void SpawnItemWithAnimation(Items itemType, float price)
    {
        GameObject prefab = GetItemPrefab(itemType);
        if (prefab == null) return;

        Vector3 originalPrefabScale = prefab.transform.localScale;

        Vector3 randomOffset = new Vector3(Random.Range(-0.15f, 0.15f), 0.05f, Random.Range(-0.15f, 0.15f));
        Vector3 spawnPos = itemPosition.position + randomOffset;

        GameObject newItem = Instantiate(prefab, spawnPos, Quaternion.identity);
        newItem.AddComponent<ItemPrice>().Price = price;
        newItem.transform.localScale = Vector3.zero;

        newItem.transform.DOScale(originalPrefabScale, 0.4f)
            .SetEase(Ease.OutBack)
            .OnComplete(() =>
            {
                newItem.transform.DOPunchScale(originalPrefabScale * 0.15f, 0.2f, 5, 1);
            });

        if (newItem.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.AddForce(Vector3.up * 2f, ForceMode.Impulse);
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
        cashregisterText.gameObject.SetActive(false);
        totalPriceText.text = "";
        totalPrice = 0;

        itemsQueue.Clear();
    }
    public void PaymentTextCash(string message)
    {
        cashregisterText.gameObject.SetActive(true);
        cashregisterText.text = message;
    }

    [ContextMenu("Finish Customer")]
    public void FinishCustomer()
    {
        if (queue.Count == 0) return;

        var npc = queue[0];

        LeaveQueue(npc);

        npc.GoAway();
    }
    public void FinalizeTransaction()
    {
        FinishCustomer();
        FinishPayment();
    }

    public void ApplyPenalty()
    {
        NpcTraject currentCustomer = GetCurrentCustomer();

        if (currentCustomer != null)
        {
            NpcInstance instance = currentCustomer.GetComponent<NpcInstance>();
            if (instance != null)
            {
                instance.ReceiveWrongChange(30);
            }
        }
    }
}