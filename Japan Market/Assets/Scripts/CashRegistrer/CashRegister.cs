using UnityEngine;
using System.Collections.Generic;
using TMPro;
using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine.UI;

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

    [Header("Item Click")]
    [Tooltip("Raio de tolerância do SphereCast usado para detectar o item clicado.")]
    [SerializeField] float clickRadius = 0.05f;

    // State
    private Camera mainCamera;
    private float zoomOri;
    private float totalPrice;
    private bool cashMode;
    private Queue<Item> itemsQueue = new Queue<Item>();
    private List<NpcTraject> queue = new List<NpcTraject>();

    public override void Awake()
    {
        base.Awake();
        ServiceLocator.Register(this);
    }

    void Start()
    {
        mainCamera = Camera.main;

        creditCard.SetActive(false);
        money.SetActive(false);

        quitButton.transform.localScale = Vector3.one;
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

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ExitCashMode();
        }
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

        ServiceLocator.Get<ItemRaycastController>().SetGeneralCanInteract(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        quitButton.SetActive(false);
        reticle.SetActive(false);

        // Parte do FOV atual e vai comprimindo até o zoom alvo
        float currentFov = cam.Lens.FieldOfView;

        DOTween.Sequence()
            .Append(playerMotor.transform
                .DOMove(cashPosition.position, 0.6f)
                .SetEase(Ease.OutQuart))

            // FOV aperta junto com o movimento — sensação de "puxar" a câmera
            .Join(DOTween.To(
                () => cam.Lens.FieldOfView,
                x => cam.Lens.FieldOfView = x,
                zoom - 3f, 0.55f)
                .SetEase(Ease.InQuart))

            // Pequeno recuo no FOV ao chegar — respira
            .Append(DOTween.To(
                () => cam.Lens.FieldOfView,
                x => cam.Lens.FieldOfView = x,
                zoom, 0.25f)
                .SetEase(Ease.OutSine))

            // ENTER — botão
            .AppendCallback(() =>
            {
                quitButton.transform.DOKill(true);
                quitButton.transform.localScale = Vector3.zero;
                quitButton.SetActive(true);
                quitButton.transform
                    .DOScale(new Vector3(0.9314623f, 4.4853f, 4.4853f), 0.35f)
                    .SetEase(Ease.OutBack);
            });
    }

    public void ExitCashMode()
    {
        cashMode = false;

        playerMotor.SetCanMove(true);
        playerLook.CanLook = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        reticle.SetActive(false);

        ServiceLocator.Get<ItemRaycastController>().SetGeneralCanInteract(true);

        totalPrice = 0;
        paymentMoney?.ClosePaymentMoney();
        paymentCard?.ClosePaymentCredi();

        DOTween.Sequence()
            // Botão encolhe antes de tudo
            .Append(quitButton.transform
                .DOScale(Vector3.zero, 0.2f)
                .SetEase(Ease.InBack))
            .AppendCallback(() => quitButton.SetActive(false))

            .Append(DOTween.To(
                () => cam.Lens.FieldOfView,
                x => cam.Lens.FieldOfView = x,
                zoomOri + 8f, 0.15f)
                .SetEase(Ease.OutQuart))

            .Append(DOTween.To(
                () => cam.Lens.FieldOfView,
                x => cam.Lens.FieldOfView = x,
                zoomOri, 0.4f)
                .SetEase(Ease.OutSine))

            .AppendCallback(() => reticle.SetActive(true));
    }

    // -------------------------
    // Items
    // -------------------------
    private void ItemClicked()
    {
        if (!TryGetClickedItem(out Item item)) return;

        if (!item.PassedItem() && itemsQueue.Contains(item))
        {
            RemoveFromQueue(item);
            SendItemToBag(item);
        }
    }

    // SphereCastAll (em vez de Raycast) em uma câmera cacheada: tolera tanto
    // pequenos desvios de mira quanto colliders da mobília/área de scan (ex.: o
    // trigger do próprio balcão) que ficariam entre a câmera e o item e, sendo
    // o hit mais próximo, fariam um Raycast simples nunca alcançar o Item.
    private bool TryGetClickedItem(out Item item)
    {
        item = null;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.SphereCastAll(ray, clickRadius);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        Debug.DrawRay(ray.origin, ray.direction * 10f, Color.red, 0.5f);
        foreach (var hit in hits)
        {
            if (hit.collider.TryGetComponent(out item))
                return true;
        }

        return false;
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
            priceItemText.text = "¥" + Mathf.RoundToInt(price);
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
        totalPriceText.text = "Total ¥" + Mathf.RoundToInt(totalPrice);
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
        Item itemComponent = newItem.GetComponent<Item>();
        if (itemComponent != null)
            itemsQueue.Enqueue(itemComponent);
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
        {
            itemsQueue.Enqueue(item);
        }
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