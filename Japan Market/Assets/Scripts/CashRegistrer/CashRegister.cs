using UnityEngine;
using System.Collections.Generic;
using TMPro;
using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine.UI;

/// <summary>
/// State machine that orchestrates item scanning, payment selection and the NPC queue.
/// States: Idle → Scanning → WaitingPayment → (back to Scanning for next customer)
/// </summary>
public class CashRegister : InteractableBase
{
    // ── Tutorial event IDs ────────────────────────────────────────────────────
    private const string EnteredCashRegisterEventId = "EnteredCashRegister";
    private const string PassedAllProductsEventId   = "PassedAllProducts";
    private const string FinishTutorialEventId      = "FinishedTutorial";

    // ── State ─────────────────────────────────────────────────────────────────
    private enum State { Idle, Scanning, WaitingPayment }
    private State _state = State.Idle;

    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("References")]
    [SerializeField] private List<AllIThingsData> allItem;
    [SerializeField] private PlayerMotor          playerMotor;
    [SerializeField] private PlayerLook           playerLook;
    [SerializeField] private PaymentMoney         paymentMoney;
    [SerializeField] private PaymentCard          paymentCard;
    [SerializeField] private GameObject           creditCard;
    [SerializeField] private GameObject           money;
    [SerializeField] private GameObject           quitButton;
    [SerializeField] private GameObject           reticle;

    [Header("UI")]
    [SerializeField] private TextMeshPro nameItemText;
    [SerializeField] private TextMeshPro priceItemText;
    [SerializeField] private TextMeshPro totalPriceText;
    [SerializeField] private TextMeshPro cashregisterText;

    [Header("Positions")]
    [SerializeField] private Transform cashPosition;
    [SerializeField] private Transform bagPoint;
    [SerializeField] private Transform bagTopPoint;
    public  Transform   itemPosition;
    public  Transform[] queuePoints;

    [Header("Camera")]
    [SerializeField] private CinemachineCamera cam;
    [SerializeField] private int              activeCameraPriority = 10;
    [SerializeField] private float            cameraBlendDuration = 0.6f;
    [SerializeField] private float            cameraLookSensitivity = 2f;
    [SerializeField] private float             zoom = 25f;

    [Header("Cash Register Camera Limits")]
    [SerializeField] private float minimumCameraXRotation = -35f;
    [SerializeField] private float maximumCameraXRotation = 35f;
    [SerializeField] private float minimumCameraYRotation = 240f;
    [SerializeField] private float maximumCameraYRotation = 304f;

    [Header("Item Scan")]
    [Tooltip("SphereCast radius for item click detection.")]
    [SerializeField] private float clickRadius = 0.05f;

    [Header("Coin Pop")]
    [SerializeField] private GameObject _coinPopPrefab;
    [SerializeField] private Transform  _popupSpawnPoint;

    // ── Runtime ───────────────────────────────────────────────────────────────
    private Camera           mainCamera;
    private CinemachineBrain cameraBrain;
    private CinemachinePanTilt cameraPanTilt;
    private CinemachineBlendDefinition originalBlend;
    private bool             isCameraBlendOverridden;
    private float            zoomOri;
    private float            totalPrice;
    private int              _totalExpected;  // items the NPC brought
    private int              _scannedCount;   // items successfully scanned
    private Queue<Item>      itemsQueue = new();
    private List<NpcTraject> npcQueue   = new();
    private bool             _tutorialFinished;

    [SerializeField] Transform coinsPosition;
    [SerializeField] Transform billPositions;
    [SerializeField] float yAmmountPerCoin;
    [SerializeField] float yAmmountPerBill;
    int coinCount = 0;
    int billCount = 0;

    public Vector3 GetMoneyPosition(MoneyType moneyType)
    {
        if (moneyType == MoneyType.Coin)
            return coinsPosition.position + new Vector3(0, yAmmountPerCoin * coinCount++, 0);
        else
            return billPositions.position + new Vector3(0, yAmmountPerBill * billCount++, 0);
    }

    public override void Awake()
    {
        base.Awake();
        ServiceLocator.Register(this);
    }

    private void Start()
    {
        mainCamera = Camera.main;
        cameraBrain = mainCamera.GetComponent<CinemachineBrain>();
        cameraPanTilt = cam.GetComponent<CinemachinePanTilt>();
        if (cameraPanTilt == null)
            cameraPanTilt = cam.gameObject.AddComponent<CinemachinePanTilt>();

        ConfigureCashCameraLook();
        HidePaymentObjects();
        cashregisterText.gameObject.SetActive(false);
        ClearTexts();
        quitButton.SetActive(false);
        zoomOri = cam.Lens.FieldOfView;
    }

    private void Update()
    {
        if (_state == State.Idle) return;

        if (Input.GetKeyDown(KeyCode.Escape)) { ExitCashMode(); return; }
        UpdateCashCameraLook();
        if (!Input.GetMouseButtonDown(0))     return;

        switch (_state)
        {
            case State.Scanning:       TryScanItem();    break;
            case State.WaitingPayment: TryOpenPayment(); break;
        }
    }

    // ── InteractableBase ──────────────────────────────────────────────────────

    public override void Interact()
    {
        if (_state == State.Idle) EnterCashMode();
    }

    // ── NPC Queue ─────────────────────────────────────────────────────────────

    public void EnterQueue(NpcTraject npc)
    {
        npcQueue.Add(npc);
        RefreshQueuePositions();
    }

    public void LeaveQueue(NpcTraject npc)
    {
        npcQueue.Remove(npc);
        RefreshQueuePositions();
    }

    private void RefreshQueuePositions()
    {
        for (int i = 0; i < npcQueue.Count; i++)
            npcQueue[i].SetTarget(queuePoints[i], i);
    }

    public NpcTraject GetCurrentCustomer() => npcQueue.Count > 0 ? npcQueue[0] : null;

    // ── Cash Mode ─────────────────────────────────────────────────────────────

    private void EnterCashMode()
    {
        _state = State.Scanning;
        NotifyTutorial(EnteredCashRegisterEventId);

        playerMotor.SetCanMove(false);
        SetCashCameraBlend();
        cam.Priority = activeCameraPriority;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        reticle.SetActive(true);
        quitButton.SetActive(false);

        DOTween.Sequence()
            .Append(FOVTween(zoom - 3f, 0.55f, Ease.InQuart))
            .Append(FOVTween(zoom, 0.25f, Ease.OutSine))
            .AppendCallback(AnimateQuitButtonIn);
    }

    public void ExitCashMode()
    {
        _state = State.Idle;

        playerMotor.SetCanMove(true);
        SetCashCameraBlend();
        cam.Priority = 0;
        DOVirtual.DelayedCall(cameraBlendDuration, RestoreCameraBlend);
        Cursor.lockState   = CursorLockMode.Locked;
        Cursor.visible     = false;

        if (paymentMoney != null) paymentMoney.Close();
        if (paymentCard  != null) paymentCard.Close();
        totalPrice = 0;

        DOTween.Sequence()
            .Append(quitButton.transform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack))
            .AppendCallback(() => quitButton.SetActive(false))
            .Append(FOVTween(zoomOri + 8f, 0.15f, Ease.OutQuart))
            .Append(FOVTween(zoomOri, 0.40f, Ease.OutSine))
            .AppendCallback(() => reticle.SetActive(true));
    }

    // ── Scanning ──────────────────────────────────────────────────────────────

    private void TryScanItem()
    {
        if (!TryGetClickedItem(out Item item)) return;
        if (item.PassedItem() || !itemsQueue.Contains(item)) return;

        DequeueItem(item);
        SendItemToBag(item);
    }

    // SphereCastAll tolerates small aiming offsets and items partially behind
    // the counter trigger collider that would stop a plain Raycast.
    private bool TryGetClickedItem(out Item item)
    {
        item = null;
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.SphereCastAll(ray, clickRadius);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
            if (hit.collider.TryGetComponent(out item)) return true;

        return false;
    }

    private void DequeueItem(Item item)
    {
        var rebuilt = new Queue<Item>();
        foreach (var i in itemsQueue)
            if (i != item) rebuilt.Enqueue(i);
        itemsQueue = rebuilt;
    }

    private void SendItemToBag(Item item)
    {
        item.MarkAsPast();
        if (item.TryGetComponent(out Collider col)) col.enabled = false;
        if (item.TryGetComponent(out Rigidbody rb))
        { rb.isKinematic = true; rb.useGravity = false; }

        DOTween.Sequence()
            .Append(item.transform.DOPunchScale(Vector3.one * 0.18f, 0.15f, 6, 1))
            .Append(item.transform.DOMoveY(item.transform.position.y + 0.32f, 0.12f).SetEase(Ease.OutQuad))
            .Append(item.transform.DOMove(bagTopPoint.position, 0.18f).SetEase(Ease.InOutQuad))
            .Append(item.transform.DOMove(bagPoint.position,    0.19f).SetEase(Ease.InQuad))
            .Append(item.transform.DOPunchScale(Vector3.one * 0.12f, 0.26f, 5))
            .AppendCallback(() => { item.transform.SetParent(bagPoint); RegisterScannedItem(item); })
            .AppendCallback(() => Destroy(item.gameObject));
    }

    private void RegisterScannedItem(Item item)
    {
        foreach (var data in allItem)
        {
            if (data.itemType != item.GetItemType()) continue;

            float price        = item.GetComponent<ItemPrice>().Price;
            nameItemText.text  = data.itemName;
            priceItemText.text = "¥" + Mathf.RoundToInt(price);
            totalPrice        += price;
            break;
        }

        _scannedCount++;

        // Use expected count, not queue size, to avoid race conditions when
        // items are clicked faster than their bag animation completes.
        if (_scannedCount >= _totalExpected && _totalExpected > 0)
            OnAllItemsScanned();
    }

    private void OnAllItemsScanned()
    {
        NotifyTutorial(PassedAllProductsEventId);
        _state = State.WaitingPayment;
        Invoke(nameof(ShowTotalAndPaymentOptions), 0.34f);
    }

    private void ShowTotalAndPaymentOptions()
    {
        ClearTexts();
        totalPriceText.text = "Total ¥" + Mathf.RoundToInt(totalPrice);

        var customer = GetCurrentCustomer();
        if (customer == null) return;

        bool card = customer.GetComponent<NpcInstance>().paymentType == PaymentType.Card;
        creditCard.SetActive(card);
        money.SetActive(!card);
    }

    // ── Payment Selection ─────────────────────────────────────────────────────
    // Routes mouse clicks to the correct payment handler based on which object
    // the player clicked. Uses RaycastAll + GetComponentInParent so clicks land
    // even when the collider is on a child of the money/card GameObject.

    private void TryOpenPayment()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        // SphereCastAll: same detection used for items — more forgiving than RaycastAll
        // with thin colliders and objects partially behind the counter trigger.
        RaycastHit[] hits = Physics.SphereCastAll(ray, clickRadius);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            // Find handler directly from hit — avoids crash if serialized refs are null.
            var pm = hit.collider.GetComponentInParent<PaymentMoney>();
            if (pm != null && !pm.IsOpen)
            {
                pm.Open(totalPrice);
                return;
            }

            var pc = hit.collider.GetComponentInParent<PaymentCard>();
            if (pc != null && !pc.IsOpen)
            {
                pc.Open(totalPrice);
                return;
            }
        }
    }

    // ── Item Spawn ────────────────────────────────────────────────────────────
    // Items are added to the queue here only. OnTriggerEnter is intentionally
    // absent to prevent the same item being enqueued twice.

    public void SpawnItemWithAnimation(Items itemType, float price)
    {
        GameObject prefab = GetItemPrefab(itemType);
        if (prefab == null) return;

        Vector3 originalScale = prefab.transform.localScale;
        Vector3 offset = new Vector3(
            Random.Range(-0.15f, 0.15f), 0.05f,
            Random.Range(-0.15f, 0.15f));

        GameObject newItem = Instantiate(prefab, itemPosition.position + offset, Quaternion.identity);
        newItem.AddComponent<ItemPrice>().Price = price;
        newItem.transform.localScale = Vector3.zero;

        newItem.transform.DOScale(originalScale, 0.4f)
            .SetEase(Ease.OutBack)
            .OnComplete(() => newItem.transform.DOPunchScale(originalScale * 0.15f, 0.2f, 5, 1));

        if (newItem.TryGetComponent<Rigidbody>(out var rb))
            rb.AddForce(Vector3.up * 2f, ForceMode.Impulse);

        if (newItem.TryGetComponent(out Item itemComponent))
        {
            itemsQueue.Enqueue(itemComponent);
            _totalExpected++;
        }
    }

    // ── Payment API (called by PaymentMoney / PaymentCard) ────────────────────

    public float GetTotalPrice() => totalPrice;

    /// <summary>
    /// Registers a physical note/coin placed on the counter as change.
    /// </summary>
    public bool AddMoney(float value)
    {
        if (paymentMoney == null || !paymentMoney.IsOpen || value <= 0f) return false;

        paymentMoney.AddMoney(value);
        return true;
    }

    /// <summary>
    /// Removes a physical note/coin from the change currently being given.
    /// </summary>
    public void RemoveMoney(float value)
    {
        if (paymentMoney == null || !paymentMoney.IsOpen) return;

        paymentMoney.RemoveMoney(value);
    }

    public void PaymentTextCash(string message)
    {
        cashregisterText.gameObject.SetActive(true);
        cashregisterText.text = message;
    }

    public void ApplyPenalty()
    {
        var customer = GetCurrentCustomer();
        if (customer == null) return;
        if (customer.TryGetComponent(out NpcInstance instance))
            instance.ReceiveWrongChange(30);
    }

    public void FinalizeTransaction()
    {
        float earned = totalPrice;
        FinishCurrentCustomer();
        ResetForNextCustomer();
        SpawnCoinPop(earned);

        if (!_tutorialFinished)
        {
            _tutorialFinished = true;
            NotifyTutorial(FinishTutorialEventId);
        }
    }

    public void ClearForCustomerLeave()
    {
        foreach (var item in itemsQueue)
            if (item != null) Destroy(item.gameObject);

        paymentMoney?.Close();
        paymentCard?.Close();
        ResetForNextCustomer();
        nameItemText.text  = "";
        priceItemText.text = "";
    }

    // ── Private Helpers ───────────────────────────────────────────────────────

    private void FinishCurrentCustomer()
    {
        if (npcQueue.Count == 0) return;
        var npc = npcQueue[0];
        LeaveQueue(npc);
        npc.GoAway();
    }

    private void ResetForNextCustomer()
    {
        totalPrice     = 0;
        _totalExpected = 0;
        _scannedCount  = 0;
        itemsQueue.Clear();
        cashregisterText.gameObject.SetActive(false);
        ClearTexts();
        HidePaymentObjects();

        if (_state == State.WaitingPayment)
            _state = State.Scanning;
    }

    private void HidePaymentObjects()
    {
        if (creditCard) creditCard.SetActive(false);
        if (money)      money.SetActive(false);
    }

    private void ClearTexts()
    {
        totalPriceText.text = "";
        nameItemText.text   = "";
        priceItemText.text  = "";
    }

    private void SpawnCoinPop(float amount)
    {
        if (_coinPopPrefab == null || _popupSpawnPoint == null) return;
        var pop = Instantiate(_coinPopPrefab, _popupSpawnPoint.position, _popupSpawnPoint.rotation);
        if (pop.TryGetComponent(out CoinPop coinPop)) coinPop.Setup(amount);
        Destroy(pop, 4f);
    }

    private Tween FOVTween(float target, float duration, Ease ease)
        => DOTween.To(() => cam.Lens.FieldOfView, x => cam.Lens.FieldOfView = x, target, duration)
                  .SetEase(ease);

    private void ConfigureCashCameraLook()
    {
        cameraPanTilt.ReferenceFrame = CinemachinePanTilt.ReferenceFrames.World;

        var panAxis = cameraPanTilt.PanAxis;
        panAxis.Range = new Vector2(minimumCameraYRotation, maximumCameraYRotation);
        panAxis.Wrap = false;
        panAxis.Value = Mathf.Clamp(cam.transform.eulerAngles.y, panAxis.Range.x, panAxis.Range.y);
        cameraPanTilt.PanAxis = panAxis;

        var tiltAxis = cameraPanTilt.TiltAxis;
        tiltAxis.Range = new Vector2(minimumCameraXRotation, maximumCameraXRotation);
        tiltAxis.Wrap = false;
        tiltAxis.Value = Mathf.Clamp(NormalizeAngle(cam.transform.eulerAngles.x), tiltAxis.Range.x, tiltAxis.Range.y);
        cameraPanTilt.TiltAxis = tiltAxis;
    }

    private void UpdateCashCameraLook()
    {
        var panAxis = cameraPanTilt.PanAxis;
        panAxis.Value = Mathf.Clamp(
            panAxis.Value + Input.GetAxisRaw("Mouse X") * cameraLookSensitivity,
            panAxis.Range.x,
            panAxis.Range.y);
        cameraPanTilt.PanAxis = panAxis;

        var tiltAxis = cameraPanTilt.TiltAxis;
        tiltAxis.Value = Mathf.Clamp(
            tiltAxis.Value - Input.GetAxisRaw("Mouse Y") * cameraLookSensitivity,
            tiltAxis.Range.x,
            tiltAxis.Range.y);
        cameraPanTilt.TiltAxis = tiltAxis;
    }

    private void SetCashCameraBlend()
    {
        if (cameraBrain == null) return;

        if (!isCameraBlendOverridden)
        {
            originalBlend = cameraBrain.DefaultBlend;
            isCameraBlendOverridden = true;
        }

        cameraBrain.DefaultBlend = new CinemachineBlendDefinition(
            CinemachineBlendDefinition.Styles.EaseInOut,
            cameraBlendDuration);
    }

    private void RestoreCameraBlend()
    {
        if (cameraBrain == null || !isCameraBlendOverridden) return;

        cameraBrain.DefaultBlend = originalBlend;
        isCameraBlendOverridden = false;
    }

    private static float NormalizeAngle(float angle)
        => angle > 180f ? angle - 360f : angle;

    private void AnimateQuitButtonIn()
    {
        quitButton.transform.localScale = Vector3.zero;
        quitButton.SetActive(true);
        quitButton.transform
            .DOScale(new Vector3(0.9314623f, 4.4853f, 4.4853f), 0.35f)
            .SetEase(Ease.OutBack);
    }

    private void NotifyTutorial(string eventId)
        => ServiceLocator.Get<TutorialManager>()?.NotifyGameEvent(eventId);

    public GameObject GetItemPrefab(Items type)
    {
        foreach (var data in allItem)
            if (data.itemType == type) return data.itemPrefab;
        return null;
    }

    [ContextMenu("Debug: Spawn Coin Pop")]
    private void DebugSpawnCoinPop() => SpawnCoinPop(500);
}
