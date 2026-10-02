using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NpcTraject : MonoBehaviour
{
    // ─── Referências ──────────────────────────────────────────────────────────
    private NavMeshAgent _agent;
    private FurnitureManager _furnitureManager;
    private CashRegister _cashRegister;

    [Header("Dest Config")]
    [SerializeField] private float _waitTimeAtShelf = 3f;

    [Header("Compra")]
    [Tooltip("Quantidade máxima de itens que o NPC carrega na sacola, somando todas as furnitures visitadas.")]
    [SerializeField] private int _maxInventorySize = 6;
    [Tooltip("Quantidades possíveis de itens pegos por furniture visitada. Repita os valores menores pra deixá-los mais prováveis (ex.: {1,1,1,2,2,3} = 50% 1 item, ~33% 2 itens, ~17% 3 itens).")]
    [SerializeField] private int[] _itemsPerFurnitureWeights = { 1, 1, 1, 2, 2, 3 };

    [Header("Comportamento")]
    [Tooltip("Quantidade de sujeiras ativas a partir da qual o NPC desiste e vai embora.")]
    [SerializeField] private int _dirtyLeaveThreshold = 12;
    [Tooltip("Preço máximo em ¥ que o NPC aceita pagar por um item. Acima disso reclama e não compra.")]
    [SerializeField] private float _maxAcceptableItemPrice = 500f;

    [Header("Exit Config")]
    [Tooltip("Distância do Exit em que o NPC é destruído (resolve bug de aglomeração na saída).")]
    [SerializeField] private float _exitDestroyDistance = 1.5f;

    [Header("Fidget (fila)")]
    [Tooltip("Ângulo máximo em graus que o NPC olha para os lados enquanto aguarda na fila.")]
    [SerializeField] private float _fidgetRotationMax = 38f;
    [Tooltip("Intervalo médio em segundos entre cada giro de fidget.")]
    [SerializeField] private float _fidgetInterval = 2.8f;

    private Transform _exitPoint;
    private readonly List<ShoppingItem> _inventory = new List<ShoppingItem>();

    // Slot reservado na furniture atual
    private FurnitureOccupancy _currentOccupancy;
    private Vector3 _reservedSlotPosition;
    private bool _itemsPlaced = false;
    private int _queueIndex = -1;
    private Vector3 _queueTargetPosition;
    private bool _hasQueueTarget;
    private NavMeshPath _queuePath;
    public bool HasArrivedAtQueueTarget { get; private set; }

    private bool _isLeaving = false;
    private Coroutine _queueWaiter;
    private Coroutine _fidgetRoutine;
    private Tween _fidgetBobTween;
    private Tween _fidgetRotTween;
    TutorialManager _tutorialManager;

    // ─── Unity ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _tutorialManager = ServiceLocator.Get<TutorialManager>();
    }

    private void Start()
    {
        _exitPoint = GameObject.FindGameObjectWithTag("Exit").transform;
        _cashRegister = ServiceLocator.Get<CashRegister>();
        _furnitureManager = ServiceLocator.Get<FurnitureManager>();

        if (_furnitureManager == null)
            _furnitureManager = FindAnyObjectByType<FurnitureManager>();

        if (_furnitureManager == null)
        {
            Debug.LogError("[NpcTraject] FurnitureManager não encontrado!");
            GoAway();
            return;
        }

        StartCoroutine(CashRegisterWatcher());
        StartCoroutine(ShoppingRoutine());
    }

    // ─── Caixa ────────────────────────────────────────────────────────────────

    private IEnumerator CashRegisterWatcher()
    {
        while (true)
        {
            if (_isLeaving) yield break;

            bool isCurrentCustomer = _cashRegister != null && _cashRegister.GetCurrentCustomer() == this;

            if (isCurrentCustomer && HasArrivedAtQueueTarget)
            {
                _tutorialManager?.NotifyGameEvent("NpcAtCashRegister");
                _tutorialManager?.NotifyGameEvent("ClientOnCashRegister");

                if (!_itemsPlaced)
                    PlaceItemsOnCounter();
            }

            yield return new WaitForSeconds(0.5f);
        }
    }

    private void PlaceItemsOnCounter()
    {
        print("[NPC] Colocando itens no balcão.");
        _itemsPlaced = true;
        StartCoroutine(UnloadInventoryRoutine());
    }

    private IEnumerator UnloadInventoryRoutine()
    {

        yield return new WaitForSeconds(0.2f);

        int placedCount = 0;
        for (int i = 0; i < _inventory.Count;)
        {
            ShoppingItem shoppingItem = _inventory[i];
            if (!_cashRegister.SpawnItemWithAnimation(shoppingItem.Type, shoppingItem.Price))
            {
                i++;
                continue;
            }

            _inventory.RemoveAt(i);
            placedCount++;
            yield return new WaitForSeconds(0.2f);
        }

        if (_inventory.Count == 0)
            Debug.Log($"[NPC] {placedCount} item(ns) colocado(s) no balcão.");
        else
            Debug.LogError(
                $"[NPC] {placedCount} item(ns) colocado(s), mas {_inventory.Count} falharam. " +
                "Confira os erros de catálogo do CashRegister.", this);
    }

    private IEnumerator ShoppingRoutine()
    {
        yield return new WaitForSeconds(Random.Range(2f, 5f));

        var allFurnitures = _furnitureManager.GetPlacedFurnitures();

        if (allFurnitures != null && allFurnitures.Count > 0)
        {
            int quantToVisit = Random.Range(1, Mathf.Min(allFurnitures.Count + 1, 6));
            var selected = PickFurnitures(allFurnitures, quantToVisit);

            foreach (var furniture in selected)
            {
                var occupancy = furniture.GetComponent<FurnitureOccupancy>();

                if (occupancy != null)
                {
                    if (!occupancy.TryReserve(out _reservedSlotPosition))
                    {
                        Debug.Log("[NPC] Furniture lotada, pulando.");
                        continue;
                    }
                    _currentOccupancy = occupancy;
                }
                else
                {
                    _reservedSlotPosition = furniture.InteractionPosition;
                    _currentOccupancy = null;
                }

                yield return StartCoroutine(GoToDest(_reservedSlotPosition));

                if (furniture.shelf != null)
                    CollectItemsFromShelf(furniture.shelf);

                yield return new WaitForSeconds(_waitTimeAtShelf);

                _currentOccupancy?.Release(_reservedSlotPosition);
                _currentOccupancy = null;

                if (Clean.ActiveDustCount >= _dirtyLeaveThreshold)
                {
                    Debug.Log("[NPC] Loja suja demais, indo embora.");
                    yield return new WaitForSeconds(2f);
                    GoAway();
                    yield break;
                }
            }
        }

        if (_inventory.Count == 0)
        {
            Debug.Log("[NPC] Sem itens (prateleiras vazias), indo embora.");
            yield return new WaitForSeconds(2f);
            GoAway();
            yield break;
        }

        // Full or temporarily blocked: retry without sharing somebody else's
        // slot, and leave cleanly if no safe space becomes available.
        float queueWait = 0f;
        while (!_isLeaving && _cashRegister != null && !_cashRegister.TryEnterQueue(this))
        {
            if (queueWait >= 90f) { GoAway(); yield break; }
            queueWait += 0.5f;
            yield return new WaitForSeconds(0.5f);
        }
        if (_cashRegister == null) GoAway();
    }

    // ─── Saída da loja ────────────────────────────────────────────────────────

    public void GoAway()
    {
        if (_isLeaving) return;
        _isLeaving = true;

        StopFidget();
        StopAllCoroutines();
        _queueWaiter = null;
        _hasQueueTarget = false;
        HasArrivedAtQueueTarget = false;
        if (_cashRegister != null) _cashRegister.LeaveQueue(this);
        _currentOccupancy?.Release(_reservedSlotPosition);
        _currentOccupancy = null;

        if (_queueWaiter != null)
        {
            StopCoroutine(_queueWaiter);
            _queueWaiter = null;
        }

        if (_agent == null || !_agent.isActiveAndEnabled || !_agent.isOnNavMesh || _exitPoint == null)
        {
            Destroy(gameObject);
            return;
        }
        _agent.isStopped = false;
        StartCoroutine(LeaveRoutine());
    }

    private IEnumerator LeaveRoutine()
    {
        _agent.SetDestination(_exitPoint.position);

        yield return new WaitForSeconds(0.3f);
        yield return new WaitUntil(() => !_agent.pathPending);

        while (true)
        {
            float dist = Vector3.Distance(transform.position, _exitPoint.position);
            if (dist <= _exitDestroyDistance) break;

            if (!_agent.hasPath || _agent.isStopped)
            {
                _agent.SetDestination(_exitPoint.position);
            }

            yield return null;
        }

        Destroy(gameObject);
    }


    public void SetQueueTarget(Transform target, int index)
    {
        if (target != null) SetQueueTarget(target.position, index);
    }

    public bool CanReachQueuePosition(Vector3 position)
    {
        if (_agent == null) _agent = GetComponent<NavMeshAgent>();
        _queuePath ??= new NavMeshPath();
        return !_isLeaving && _agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh
            && _agent.CalculatePath(position, _queuePath)
            && _queuePath.status == NavMeshPathStatus.PathComplete;
    }

    public void SetQueueTarget(Vector3 target, int index)
    {
        if (_isLeaving) return;
        // Periodic validation and arrivals behind this NPC must not restart its
        // movement, idle animation or waiter when the slot hasn't changed.
        if (_hasQueueTarget && _queueIndex == index
            && (target - _queueTargetPosition).sqrMagnitude < 0.0001f
            && (HasArrivedAtQueueTarget || _queueWaiter != null)) return;

        _queueIndex = index;
        _queueTargetPosition = target;
        _hasQueueTarget = true;

        StopFidget();

        if (_queueWaiter != null)
            StopCoroutine(_queueWaiter);

        HasArrivedAtQueueTarget = false;
        bool reachable = CanReachQueuePosition(target);
        if (reachable)
        {
            _agent.isStopped = false;
            reachable = _agent.SetPath(_queuePath);
        }
        _queueWaiter = StartCoroutine(WaitUntilAtQueuePosition(target, reachable));
    }

    private IEnumerator WaitUntilAtQueuePosition(Vector3 target, bool reachable)
    {
        // Do not remove an NPC synchronously inside CashRegister's refresh loop.
        yield return null;
        if (!reachable)
        {
            _queueWaiter = null;
            GoAway();
            yield break;
        }

        float elapsed = 0f;
        const float timeout = 15f;

        bool arrived = false;
        while (elapsed < timeout)
        {
            if (_agent == null || !_agent.isActiveAndEnabled || !_agent.isOnNavMesh) break;
            if (!_agent.pathPending)
            {
                if (_agent.pathStatus != NavMeshPathStatus.PathComplete) break;
                Vector3 delta = transform.position - target;
                delta.y = 0f;
                float threshold = Mathf.Max(0.15f, _agent.stoppingDistance);
                if (_agent.remainingDistance <= threshold
                    && delta.sqrMagnitude <= (threshold + 0.1f) * (threshold + 0.1f))
                {
                    arrived = true;
                    break;
                }
                if (!_agent.hasPath || _agent.isStopped) break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        _queueWaiter = null;
        if (!arrived)
        {
            GoAway();
            yield break;
        }

        _agent.isStopped = true;
        HasArrivedAtQueueTarget = true;

        StartFidget();
    }
    public void SetTarget(Transform target, int index) => SetQueueTarget(target, index);

    private void OnDisable()
    {
        StopFidget();
        StopAllCoroutines();
        _queueWaiter = null;
        _hasQueueTarget = false;
        HasArrivedAtQueueTarget = false;
        if (_cashRegister != null) _cashRegister.LeaveQueue(this);
    }

    // ─── Fidget (animação de espera na fila) ──────────────────────────────────

    private void StartFidget()
    {
        StopFidget();

        float baseY = transform.position.y;
        _fidgetBobTween = transform.DOMoveY(baseY + 0.035f, Random.Range(0.9f, 1.3f))
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);

        _fidgetRoutine = StartCoroutine(FidgetRoutine());
    }

    private void StopFidget()
    {
        if (_fidgetRoutine != null) { StopCoroutine(_fidgetRoutine); _fidgetRoutine = null; }
        _fidgetBobTween?.Kill();
        _fidgetRotTween?.Kill();
    }

    private IEnumerator FidgetRoutine()
    {
        Quaternion baseRot = transform.rotation;

        while (true)
        {
            yield return new WaitForSeconds(Random.Range(_fidgetInterval * 0.55f, _fidgetInterval * 1.45f));

            float yAngle = Random.Range(-_fidgetRotationMax, _fidgetRotationMax);
            float lookTime = Random.Range(0.45f, 0.85f);
            _fidgetRotTween = transform.DORotateQuaternion(baseRot * Quaternion.Euler(0f, yAngle, 0f), lookTime)
                .SetEase(Ease.InOutSine);
            yield return _fidgetRotTween.WaitForCompletion();

            yield return new WaitForSeconds(Random.Range(0.5f, 1.6f));

            float returnTime = Random.Range(0.35f, 0.65f);
            _fidgetRotTween = transform.DORotateQuaternion(baseRot, returnTime)
                .SetEase(Ease.InOutSine);
            yield return _fidgetRotTween.WaitForCompletion();
        }
    }

    // ─── Movimento genérico ───────────────────────────────────────────────────

    private IEnumerator GoToDest(Vector3 dest)
    {
        _agent.SetDestination(dest);

        yield return new WaitUntil(() => !_agent.pathPending);

        float arrivalThreshold = _agent.stoppingDistance + 0.05f;

        // Timeout evita que o NPC fique preso pra sempre num destino inalcançável
        // (slot bloqueado, NavMesh com buraco, etc.) — sem isso ele travava a rotina inteira.
        float elapsed = 0f;
        const float timeout = 15f;

        while (_agent.remainingDistance > arrivalThreshold && elapsed < timeout)
        {
            if (_agent.pathStatus == NavMeshPathStatus.PathInvalid) break;
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    // ─── Seleção de furnitures ────────────────────────────────────────────────

    private List<FurnitureInstance> PickFurnitures(List<FurnitureInstance> source, int count)
    {
        var copy = new List<FurnitureInstance>(source);
        var result = new List<FurnitureInstance>();

        for (int i = 0; i < count && copy.Count > 0; i++)
        {
            int idx = Random.Range(0, copy.Count);
            result.Add(copy[idx]);
            copy.RemoveAt(idx);
        }

        return result;
    }

    // ─── Compras / Inventário ─────────────────────────────────────────────────
    private void CollectItemsFromShelf(Shelf shelf)
    {
        var globalPrices = ServiceLocator.Get<GlobalPrices>();
        int quantity = RollItemQuantity();

        for (int i = 0; i < quantity && _inventory.Count < _maxInventorySize; i++)
        {
            Items peekedType = shelf.PeekRandomItemType();
            if (peekedType == Items.None) break;

            float price = globalPrices.GetItemCurrentPrice(peekedType);

            if (price > _maxAcceptableItemPrice)
                break;

            Items taken = shelf.TakeItemOfType(peekedType);
            if (taken == Items.None) break;

            _inventory.Add(new ShoppingItem(taken, price));
        }
    }

    private int RollItemQuantity()
    {
        if (_itemsPerFurnitureWeights == null || _itemsPerFurnitureWeights.Length == 0)
            return 1;

        return _itemsPerFurnitureWeights[Random.Range(0, _itemsPerFurnitureWeights.Length)];
    }

    private readonly struct ShoppingItem
    {
        public readonly Items Type;
        public readonly float Price;

        public ShoppingItem(Items type, float price)
        {
            Type = type;
            Price = price;
        }
    }
}
