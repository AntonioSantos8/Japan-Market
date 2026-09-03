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
    private bool             _inCardMachineMode;
    private CinemachineCamera _activeMachineCamera;
    private float            zoomOri;
    private float            totalPrice;
    private int              _totalExpected;  // items the NPC brought
    private int              _scannedCount;   // items successfully scanned
    private Queue<Item>      itemsQueue = new();
    private List<NpcTraject> npcQueue   = new();
    private List<MoneyInstance> spawnedMoney = new();
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

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_inCardMachineMode) ExitCardMachineMode();
            else                    ExitCashMode();
            return;
        }

        UpdateCashCameraLook();

        if (_state == State.WaitingPayment && paymentMoney != null && paymentMoney.IsOpen
            && Input.GetKeyDown(KeyCode.Space))
        {
            paymentMoney.Confirm();
            return;
        }

        if (!Input.GetMouseButtonDown(0)) return;

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

    public PaymentType GetCurrentPaymentType()
    {
        var customer = GetCurrentCustomer();
        if (customer == null || !customer.TryGetComponent(out NpcInstance instance))
            return PaymentType.Cash;

        return instance.paymentType;
    }

    public bool IsCardPayment()
    {
        return GetCurrentPaymentType() == PaymentType.Card;
    }

    public bool IsCashPayment()
    {
        return GetCurrentPaymentType() == PaymentType.Cash;
    }

    // ── Cash Mode ─────────────────────────────────────────────────────────────

    private void EnterCashMode()
    {
        // Resume para WaitingPayment se o cliente atual já tinha terminado de ser escaneado
        // antes do jogador sair do modo caixa (Esc) — senão o pagamento nunca reaparece e o
        // ciclo trava esperando o cliente desistir.
        bool scanningAlreadyDone = _totalExpected > 0 && _scannedCount >= _totalExpected && GetCurrentCustomer() != null;
        _state = scanningAlreadyDone ? State.WaitingPayment : State.Scanning;
        NotifyTutorial(EnteredCashRegisterEventId);

        playerMotor.SetCanMove(false);
        SetCashCameraBlend();
        cam.Priority = activeCameraPriority;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        reticle.SetActive(true);
        quitButton.SetActive(false);

        if (_state == State.WaitingPayment)
            ShowTotalAndPaymentOptions();

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
        HidePaymentObjects();

        DOTween.Sequence()
            .Append(quitButton.transform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack))
            .AppendCallback(() => quitButton.SetActive(false))
            .Append(FOVTween(zoomOri + 8f, 0.15f, Ease.OutQuart))
            .Append(FOVTween(zoomOri, 0.40f, Ease.OutSine))
            .AppendCallback(() => reticle.SetActive(true));
    }

    // ── Card Machine Mode ─────────────────────────────────────────────────────

    public void EnterCardMachineMode(CinemachineCamera machineCamera, int priority, PaymentCard machinePaymentCard = null)
    {
        if (_state != State.WaitingPayment || _inCardMachineMode || machineCamera == null) return;
        if (!IsCardPayment()) return;

        // A maquininha pode ter seu próprio PaymentCard (com a UI real que o jogador usa),
        // diferente do paymentCard padrão do registro — usa esse a partir daqui.
        if (machinePaymentCard != null) paymentCard = machinePaymentCard;

        if (paymentCard != null && !paymentCard.IsOpen) paymentCard.Open(totalPrice);

        _inCardMachineMode   = true;
        _activeMachineCamera = machineCamera;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetCashCameraBlend();
        machineCamera.Priority = priority;
    }

    public void ExitCardMachineMode()
    {
        if (!_inCardMachineMode) return;

        _inCardMachineMode = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        SetCashCameraBlend();
        if (_activeMachineCamera != null) _activeMachineCamera.Priority = 0;
        _activeMachineCamera = null;
        DOVirtual.DelayedCall(cameraBlendDuration, RestoreCameraBlend);
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

        var paymentType = customer.GetComponent<NpcInstance>().paymentType;
        Debug.Log($"[CashRegister] Cliente quer pagar em: {paymentType}");

        bool card = paymentType == PaymentType.Card;
        if (creditCard) creditCard.SetActive(card);
        if (money)      money.SetActive(!card);
    }

    // ── Payment Selection ─────────────────────────────────────────────────────
    // Routes mouse clicks to the correct payment handler based on which object
    // the player clicked. Uses RaycastAll + GetComponentInParent so clicks land
    // even when the collider is on a child of the money/card GameObject.

    private void TryOpenPayment()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.SphereCastAll(ray, clickRadius);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        var paymentType = GetCurrentPaymentType();

        foreach (var hit in hits)
        {
            var pm = hit.collider.GetComponentInParent<PaymentMoney>();
            if (pm != null && paymentType == PaymentType.Cash)
            {
                // Primeiro clique abre o pagamento; clicar de novo confirma o troco
                // dado até agora (só fecha a venda se o troco estiver certo).
                if (!pm.IsOpen) pm.Open(totalPrice);
                else            pm.Confirm();
                return;
            }

            var pc = hit.collider.GetComponentInParent<PaymentCard>();
            if (pc != null && paymentType == PaymentType.Card && !pc.IsOpen)
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

    public void RegisterMoneyInstance(MoneyInstance money)
    {
        if (money == null || spawnedMoney.Contains(money)) return;
        spawnedMoney.Add(money);
    }

    public void UnregisterMoneyInstance(MoneyInstance money)
    {
        if (money == null) return;
        spawnedMoney.Remove(money);
    }

    /// <summary>
    /// Registers a physical note/coin placed on the counter as change.
    /// </summary>
    public bool AddMoney(float value)
    {
        if (paymentMoney == null || value <= 0f) return false;

        if (GetCurrentPaymentType() != PaymentType.Cash)
        {
            Debug.LogWarning($"[CashRegister] Bloqueado: dinheiro foi usado enquanto o cliente paga com {GetCurrentPaymentType()}.");
            return false;
        }

        if (!paymentMoney.IsOpen)
            paymentMoney.Open(totalPrice);

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
    }

    public void FinalizeTransaction()
    {
        float earned = totalPrice;
        if (_inCardMachineMode) ExitCardMachineMode();
        FinishCurrentCustomer();
        ResetMoneyPlacementState();
        ResetForNextCustomer();
        SpawnCoinPop(earned);

        if (!_tutorialFinished)
        {
            _tutorialFinished = true;
            // Chamada direta (não NotifyGameEvent) pra garantir que o mascote some na
            // primeira venda mesmo que o tutorial ainda esteja num passo anterior.
            ServiceLocator.Get<TutorialManager>()?.SkipTutorial();
        }
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

    public void ResetMoneyPlacementState()
    {
        coinCount = 0;
        billCount = 0;

        for (int i = spawnedMoney.Count - 1; i >= 0; i--)
        {
            var money = spawnedMoney[i];
            if (money != null)
                Destroy(money.gameObject);
            spawnedMoney.RemoveAt(i);
        }
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
