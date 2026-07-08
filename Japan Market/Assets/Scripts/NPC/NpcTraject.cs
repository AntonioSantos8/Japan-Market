using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NpcTraject : MonoBehaviour
{
    // ─── Referências ──────────────────────────────────────────────────────────
    private NavMeshAgent _agent;
    private NpcInstance _npcInstance;
    private NpcPhrases _npcPhrases;
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
    [Tooltip("Tempo base (em segundos) que o NPC aguenta esperar na frente do caixa antes de desistir. É multiplicado pela paciência do humor atual.")]
    [SerializeField] private float _queuePatienceSeconds = 90f;
    [Tooltip("Quantidade de sujeiras ativas a partir da qual o NPC reclama da loja suja.")]
    [SerializeField] private int _dirtyComplainThreshold = 5;
    [Tooltip("Quantidade de sujeiras ativas a partir da qual o NPC desiste e vai embora.")]
    [SerializeField] private int _dirtyLeaveThreshold = 12;
    [Tooltip("Intervalo (em segundos) entre frases de impaciência enquanto espera no caixa.")]
    [SerializeField] private float _impatientPhraseInterval = 25f;
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

    // Snapshot dos itens colocados no balcão — usado para "devolvê-los" se o NPC desistir.
    private readonly List<ShoppingItem> _unloadedItems = new List<ShoppingItem>();
    private bool _itemsFullyUnloaded = false;

    // Slot reservado na furniture atual
    private FurnitureOccupancy _currentOccupancy;
    private Vector3 _reservedSlotPosition;
    private bool _itemsPlaced = false;
    private int _queueIndex = -1;
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
        _npcInstance = GetComponent<NpcInstance>();
        _npcPhrases = GetComponent<NpcPhrases>();
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
        float timeWaitingAtFront = 0f;
        float nextImpatientAt = _impatientPhraseInterval;

        while (true)
        {
            if (_isLeaving) yield break;

            bool isCurrentCustomer = _cashRegister.GetCurrentCustomer() == this;

            if (isCurrentCustomer && HasArrivedAtQueueTarget)
            {
                _tutorialManager?.NotifyGameEvent("NpcAtCashRegister");
                _tutorialManager?.NotifyGameEvent("ClientOnCashRegister");

                if (!_itemsPlaced)
                    PlaceItemsOnCounter();

                
                timeWaitingAtFront += 0.5f;

                if (timeWaitingAtFront >= nextImpatientAt)
                {
                    _npcPhrases?.SayEvent(NpcEvent.Impatient);
                    nextImpatientAt += _impatientPhraseInterval;
                }

                if (timeWaitingAtFront >= _queuePatienceSeconds * GetPatienceMultiplier())
                {
                    yield return StartCoroutine(GiveUpAndReturnItems());
                    yield break;
                }
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
       
        _unloadedItems.Clear();
        _unloadedItems.AddRange(_inventory);

        yield return new WaitForSeconds(0.2f);

        foreach (var shoppingItem in _inventory)
        {
            _cashRegister.SpawnItemWithAnimation(shoppingItem.Type, shoppingItem.Price);
            yield return new WaitForSeconds(0.2f);
        }

        _inventory.Clear();
        _itemsFullyUnloaded = true;
        Debug.Log("[NPC] Itens colocados no balcão.");
    }

    // ─── Desistência no caixa ─────────────────────────────────────────────────

    private float GetPatienceMultiplier()
    {
        switch (_npcInstance.CurrentHumor)
        {
            case NpcHumor.Happy:   return 1.3f;
            case NpcHumor.Neutral: return 1.0f;
            case NpcHumor.Angry:   return 0.65f;
            default:               return 1.0f;
        }
    }

    private IEnumerator GiveUpAndReturnItems()
    {
        if (_isLeaving) yield break;

        StopFidget();
        _cashRegister.LeaveQueue(this);
        _cashRegister.ClearForCustomerLeave();
        _npcPhrases?.SayEvent(NpcEvent.CashierTooSlow);

        // O agente foi parado ao chegar na fila — libera para voltar a andar.
        _agent.isStopped = false;

        yield return new WaitForSeconds(1.5f);

        // Se ainda não descarregou tudo, o inventário continua sendo a fonte da verdade;
        // caso contrário, usa o snapshot dos itens que foram para o balcão.
        var source = _itemsFullyUnloaded || _inventory.Count == 0 ? _unloadedItems : _inventory;
        var items = new List<ShoppingItem>(source);

        var frozenItems = items.Where(i => GetFurnitureTypeForItem(i.Type) == FurnitureType.Freezer).ToList();
        var shelfItems  = items.Where(i => GetFurnitureTypeForItem(i.Type) != FurnitureType.Freezer).ToList();

        yield return StartCoroutine(ReturnGroupToFurniture(frozenItems, FurnitureType.Freezer));
        yield return StartCoroutine(ReturnGroupToFurniture(shelfItems,  FurnitureType.Shelf));

        _inventory.Clear();
        _unloadedItems.Clear();
        GoAway();
    }

    private IEnumerator ReturnGroupToFurniture(List<ShoppingItem> items, FurnitureType furnitureType)
    {
        if (items.Count == 0) yield break;

        FurnitureInstance target = FindFurnitureOfType(furnitureType);
        if (target == null) yield break;

        _npcPhrases?.SayEvent(NpcEvent.ReturningItems);
        yield return StartCoroutine(GoToDest(target.InteractionPosition));
        yield return new WaitForSeconds(1.5f);
    }

    private FurnitureInstance FindFurnitureOfType(FurnitureType type)
    {
        var placed = _furnitureManager.GetPlacedFurnitures();
        if (placed == null) return null;

        var matches = placed.Where(f => f != null && f.Data != null && f.Data.type == type).ToList();
        return matches.Count > 0 ? matches[Random.Range(0, matches.Count)] : null;
    }

    private static FurnitureType GetFurnitureTypeForItem(Items item)
    {
        switch (item)
        {
            case Items.IceCream:
            case Items.FrozenMeat:
            case Items.FrozenPizza:
            case Items.Fish:
            case Items.Cola:
                return FurnitureType.Freezer;
            default:
                return FurnitureType.Shelf;
        }
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

                ReactToDirtyStore();

                if (Clean.ActiveDustCount >= _dirtyLeaveThreshold)
                {
                    Debug.Log("[NPC] Loja suja demais, indo embora.");
                    _npcPhrases?.SayEvent(NpcEvent.DirtyStore);
                    yield return new WaitForSeconds(2f);
                    GoAway();
                    yield break;
                }
            }
        }

        if (_inventory.Count == 0)
        {
            Debug.Log("[NPC] Sem itens (prateleiras vazias), indo embora.");
            _npcPhrases?.SayEvent(NpcEvent.EmptyShelves);
            yield return new WaitForSeconds(2f);
            GoAway();
            yield break;
        }

        _cashRegister.EnterQueue(this);
        Debug.Log("[NPC] Entrou na fila.");
    }

    private void ReactToDirtyStore()
    {
        int dustCount = Clean.ActiveDustCount;
        if (dustCount < _dirtyComplainThreshold) return;

        _npcPhrases?.SayEvent(NpcEvent.DirtyStore);

        // Quanto mais sujeira, maior a penalidade de humor.
        int penalty = Mathf.FloorToInt(dustCount / (float)_dirtyComplainThreshold) * 5;
        _npcInstance.ReceiveWrongChange(penalty);
    }

    // ─── Saída da loja ────────────────────────────────────────────────────────

    public void GoAway()
    {
        if (_isLeaving) return;
        _isLeaving = true;

        StopFidget();
        _currentOccupancy?.Release(_reservedSlotPosition);
        _currentOccupancy = null;

        if (_queueWaiter != null)
        {
            StopCoroutine(_queueWaiter);
            _queueWaiter = null;
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
        _queueIndex = index;
        Debug.Log($"[NPC:{name}] Nova posição na fila: {_queueIndex}");

        StopFidget(); // para o fidget antes de andar para nova posição

        if (_queueWaiter != null)
            StopCoroutine(_queueWaiter);

        HasArrivedAtQueueTarget = false;
        _agent.isStopped = false;
        _agent.SetDestination(target.position);
        _queueWaiter = StartCoroutine(WaitUntilAtQueuePosition(target));
    }

    private IEnumerator WaitUntilAtQueuePosition(Transform target)
    {
        yield return new WaitUntil(() => !_agent.pathPending);

        float elapsed = 0f;
        const float timeout = 15f;

        while ((_agent.remainingDistance > _agent.stoppingDistance
               || (_agent.hasPath && _agent.velocity.sqrMagnitude > 0.01f))
               && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (elapsed >= timeout)
            Debug.LogWarning($"[NPC:{name}] Timeout esperando chegar na fila — forçando chegada.");

        _agent.isStopped = true;
        HasArrivedAtQueueTarget = true;
        _queueWaiter = null;

        StartFidget();
    }
    public void SetTarget(Transform target, int index) => SetQueueTarget(target, index);

    // ─── Fidget (animação de espera na fila) ──────────────────────────────────

    private void StartFidget()
    {
        StopFidget();

        // Bob sutil de peso (cima e baixo) — simula respiração/nervosismo
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
            // Espera aleatória antes de olhar para o lado
            yield return new WaitForSeconds(Random.Range(_fidgetInterval * 0.55f, _fidgetInterval * 1.45f));

            float yAngle = Random.Range(-_fidgetRotationMax, _fidgetRotationMax);
            float lookTime = Random.Range(0.45f, 0.85f);
            _fidgetRotTween = transform.DORotateQuaternion(baseRot * Quaternion.Euler(0f, yAngle, 0f), lookTime)
                .SetEase(Ease.InOutSine);
            yield return _fidgetRotTween.WaitForCompletion();

            // Fica olhando por um momento
            yield return new WaitForSeconds(Random.Range(0.5f, 1.6f));

            // Volta para a direção base
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

        while (_agent.remainingDistance > arrivalThreshold)
            yield return null;
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
            // Espia o tipo do item SEM remover da prateleira
            Items peekedType = shelf.PeekRandomItemType();
            if (peekedType == Items.None) break;

            float price = globalPrices.GetItemCurrentPrice(peekedType);

            if (price > _maxAcceptableItemPrice)
            {
                // Preço alto demais: reclama e para de olhar essa prateleira — item FICA no lugar
                _npcPhrases?.SayEvent(NpcEvent.PriceTooHigh);
                _npcInstance.ReceiveWrongChange(8);
                break;
            }

            // Preço OK: agora remove o item da prateleira
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
