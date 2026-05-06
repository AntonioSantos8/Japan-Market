using UnityEngine;
using System.Collections.Generic;
using TMPro;
using DG.Tweening;
using Unity.Cinemachine;

public class CashRegister : InteractableBase
{
    [Header("References")]
    [SerializeField] List<AllIThingsData> allItem;
    [SerializeField] PlayerMotor playerMotor;
    [SerializeField] PlayerLook playerLook;
    [SerializeField] PaymentMoney paymentMoney;
    [SerializeField] PaymentCard paymentCard;
    [SerializeField] GameObject creditCard;
    [SerializeField] GameObject money;
    [SerializeField] GameObject quitButton;
    [SerializeField] GameObject reticle;

    [Header("UI")]
    [SerializeField] TextMeshPro nameItemText;
    [SerializeField] TextMeshPro priceItemText;
    [SerializeField] TextMeshPro totalPriceText;
    [SerializeField] TextMeshPro cashregisterText;

    [Header("Positions")]
    [SerializeField] Transform bagPoint;
    [SerializeField] Transform bagTopPoint;
    [SerializeField] Transform cashPosition;
    public Transform itemPosition;
    public Transform[] queuePoints;

    [Header("Camera")]
    [SerializeField] CinemachineCamera cam;
    [SerializeField] float zoom = 25f;

    // State
    private float zoomOri;
    private float totalPrice;
    private bool cashMode;
    private Queue<Item> itemsQueue = new Queue<Item>();
    private List<NpcTraject> queue = new List<NpcTraject>();
    public bool hasClient;

    void Awake()
    {
        ServiceLocator.Register(this);
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
        if (!cashMode) return;

        if (Input.GetMouseButtonDown(0))
            ItemClicked();
    }

    // -------------------------
    // InteractableBase override
    // -------------------------
    public override void Interact()
    {
        if (!cashMode)
            EnterCashMode();
    }

    // -------------------------
    // Queue
    // -------------------------
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

    private void UpdateQueue()
    {
        for (int i = 0; i < queue.Count; i++)
            queue[i].SetTarget(queuePoints[i], i);
    }

    public NpcTraject GetCurrentCustomer() => queue.Count > 0 ? queue[0] : null;

    // -------------------------
    // Cash Mode
    // -------------------------
    private void EnterCashMode()
    {
        cashMode = true;

        playerLook.ResetLook();
        playerMotor.SetCanMove(false);
        playerMotor.ResetCameraEffects();
        playerLook.CanLook = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        quitButton.SetActive(true);
        reticle.SetActive(false);

        //ServiceLocator.Get<ItemRaycastController>().SetGeneralCanInteract(false);

        DOTween.Sequence()
            .Append(playerMotor.transform.DOMove(cashPosition.position, 0.3f).SetEase(Ease.OutQuad))
            .Join(DOTween.To(
                () => cam.Lens.FieldOfView,
                x => cam.Lens.FieldOfView = x,
                zoom, 0.4f).SetEase(Ease.OutQuad));
    }

    public void ExitCashMode()
    {
        cashMode = false;

        playerMotor.SetCanMove(true);
        playerLook.CanLook = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        quitButton.SetActive(false);
        reticle.SetActive(true);

        ServiceLocator.Get<ItemRaycastController>().SetGeneralCanInteract(true);

        DOTween.To(
            () => cam.Lens.FieldOfView,
            x => cam.Lens.FieldOfView = x,
            zoomOri, 0.35f).SetEase(Ease.OutQuad);

        totalPrice = 0;
        paymentMoney?.ClosePaymentMoney();
        paymentCard?.ClosePaymentCredi();
    }

    // -------------------------
    // Items
    // -------------------------
    private void ItemClicked()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Debug.Log("1");
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;
        Debug.Log(ray);

        Debug.Log("2");

        Item item = hit.collider.GetComponent<Item>();
        Debug.Log(item);
        Debug.Log(itemsQueue);
        Debug.Log(item.PassedItem());
        if (item != null && item.PassedItem() == false && itemsQueue.Contains(item))
        {
            Debug.Log("3");

            RemoveFromQueue(item);
            SendItemToBag(item);
        }
    }

    private void RemoveFromQueue(Item item)
    {
        Queue<Item> newQueue = new Queue<Item>();
        foreach (var i in itemsQueue)
            if (i != item) newQueue.Enqueue(i);
        itemsQueue = newQueue;
    }

    private void SendItemToBag(Item item)
    {
        item.MarkAsPast();

        if (item.TryGetComponent(out Collider col)) col.enabled = false;
        if (item.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        DOTween.Sequence()
            .Append(item.transform.DOPunchScale(Vector3.one * 0.18f, 0.15f, 6, 1))
            .Append(item.transform.DOMoveY(item.transform.position.y + 0.32f, 0.12f).SetEase(Ease.OutQuad))
            .Append(item.transform.DOMove(bagTopPoint.position, 0.18f).SetEase(Ease.InOutQuad))
            .Append(item.transform.DOMove(bagPoint.position, 0.19f).SetEase(Ease.InQuad))
            .Append(item.transform.DOPunchScale(Vector3.one * 0.12f, 0.26f, 5))
            .AppendCallback(() =>
            {
                item.transform.SetParent(bagPoint);
                RegisterItem(item);
            })
            .AppendCallback(() => Destroy(item.gameObject));
    }

    private void RegisterItem(Item item)
    {
        foreach (var data in allItem)
        {
            if (data.itemType != item.GetItemType()) continue;

            float price = item.GetComponent<ItemPrice>().Price;

            nameItemText.text = data.itemName;
            priceItemText.text = "¥" + price.ToString("F2");
            totalPrice += price;

            if (itemsQueue.Count == 0)
                OnLastItemScanned();

            break;
        }
    }

    private void OnLastItemScanned()
    {
        Invoke(nameof(ShowTotal), 0.34f);

        creditCard.SetActive(false);
        money.SetActive(false);

        var customer = GetCurrentCustomer();
        if (customer == null) return;

        var instance = customer.GetComponent<NpcInstance>();
        if (instance.paymentType == PaymentType.Card)
            creditCard.SetActive(true);
        else
            money.SetActive(true);
    }

    private void ShowTotal()
    {
        nameItemText.text = "";
        priceItemText.text = "";
        totalPriceText.text = "Total ¥" + totalPrice.ToString("F2");
    }

    // -------------------------
    // Spawn
    // -------------------------
    public void SpawnItemWithAnimation(Items itemType, float price)
    {
        GameObject prefab = GetItemPrefab(itemType);
        if (prefab == null) return;

        Vector3 originalScale = prefab.transform.localScale;
        Vector3 offset = new Vector3(Random.Range(-0.15f, 0.15f), 0.05f, Random.Range(-0.15f, 0.15f));

        GameObject newItem = Instantiate(prefab, itemPosition.position + offset, Quaternion.identity);
        newItem.AddComponent<ItemPrice>().Price = price;
        newItem.transform.localScale = Vector3.zero;

        newItem.transform.DOScale(originalScale, 0.4f)
            .SetEase(Ease.OutBack)
            .OnComplete(() => newItem.transform.DOPunchScale(originalScale * 0.15f, 0.2f, 5, 1));

        if (newItem.TryGetComponent<Rigidbody>(out var rb))
            rb.AddForce(Vector3.up * 2f, ForceMode.Impulse);
    }

    // -------------------------
    // Payment
    // -------------------------
    public float GetTotalPrice() => totalPrice;

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

    public void ApplyPenalty()
    {
        var customer = GetCurrentCustomer();
        customer?.GetComponent<NpcInstance>()?.ReceiveWrongChange(30);
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

    // -------------------------
    // Trigger (só items, não player)
    // -------------------------
    void OnTriggerEnter(Collider other)
    {
        Item item = other.GetComponent<Item>();
        if (item != null && !item.PassedItem())
            itemsQueue.Enqueue(item);
    }

    // -------------------------
    // Helpers
    // -------------------------
    public GameObject GetItemPrefab(Items type)
    {
        foreach (var data in allItem)
            if (data.itemType == type) return data.itemPrefab;
        return null;
    }
}