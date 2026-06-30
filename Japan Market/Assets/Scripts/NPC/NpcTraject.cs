using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Controla a movimentação, compras e saída do NPC da loja.
/// 
/// Melhorias em relação à versão anterior:
/// - Usa FurnitureOccupancy para evitar aglomeração em prateleiras.
/// - A saída não depende de chegar num ponto exato: destrói o NPC por distância
///   do exit, resolvendo o bug de múltiplos NPCs travados no exit.
/// - Animação agora é gerenciada automaticamente por NpcAnimationManager via Update,
///   mas SetTarget e GoAway ainda podem forçar se necessário.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class NpcTraject : MonoBehaviour
{
    // ─── Referências ──────────────────────────────────────────────────────────
    private NavMeshAgent _agent;
    private NpcInstance _npcInstance;
    private FurnitureManager _furnitureManager;
    private CashRegister _cashRegister;

    [Header("Dest Config")]
    [SerializeField] private float _waitTimeAtShelf = 3f;

    [Header("Compra")]
    [Tooltip("Quantidade máxima de itens que o NPC carrega na sacola, somando todas as furnitures visitadas.")]
    [SerializeField] private int _maxInventorySize = 6;
    [Tooltip("Quantidades possíveis de itens pegos por furniture visitada. Repita os valores menores pra deixá-los mais prováveis (ex.: {1,1,1,2,2,3} = 50% 1 item, ~33% 2 itens, ~17% 3 itens).")]
    [SerializeField] private int[] _itemsPerFurnitureWeights = { 1, 1, 1, 2, 2, 3 };

    [Header("Exit Config")]
    [Tooltip("Distância do Exit em que o NPC é destruído (resolve bug de aglomeração na saída).")]
    [SerializeField] private float _exitDestroyDistance = 1.5f;

    // Referência ao exit point
    private Transform _exitPoint;

    // Inventário: cada item pego, com o preço no momento da compra.
    // É uma lista (não um Dictionary) porque o NPC pode pegar o mesmo
    // tipo de item mais de uma vez.
    private readonly List<ShoppingItem> _inventory = new List<ShoppingItem>();

    // Slot reservado na furniture atual
    private FurnitureOccupancy _currentOccupancy;
    private Vector3 _reservedSlotPosition;

    // Controle de caixa
    private bool _itemsPlaced = false;
    private int _queueIndex = -1;

    // True quando este NPC já chegou (parou) na posição de fila que recebeu.
    // É dono do próprio estado de chegada — não depende de trigger compartilhado.
    public bool HasArrivedAtQueueTarget { get; private set; }

    private bool _isLeaving = false;
    private Coroutine _queueWaiter;

    // ─── Unity ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _npcInstance = GetComponent<NpcInstance>();
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
            if (!_itemsPlaced
                && _cashRegister.GetCurrentCustomer() == this
                && HasArrivedAtQueueTarget)
            {
                PlaceItemsOnCounter();
            }
            yield return new WaitForSeconds(0.5f);
        }
    }

    private void PlaceItemsOnCounter()
    {
        _itemsPlaced = true;
        StartCoroutine(UnloadInventoryRoutine());
    }

    private IEnumerator UnloadInventoryRoutine()
    {
        yield return new WaitForSeconds(0.2f);

        foreach (var shoppingItem in _inventory)
        {
            _cashRegister.SpawnItemWithAnimation(shoppingItem.Type, shoppingItem.Price);
            yield return new WaitForSeconds(0.2f);
        }

        _inventory.Clear();
        Debug.Log("[NPC] Itens colocados no balcão.");
    }

    // ─── Rotina principal de compras ──────────────────────────────────────────

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
            }
        }

        if (_inventory.Count == 0)
        {
            Debug.Log("[NPC] Sem itens, indo embora.");
            GoAway();
            yield break;
        }

        _cashRegister.EnterQueue(this);
        Debug.Log("[NPC] Entrou na fila.");
    }

    // ─── Saída da loja ────────────────────────────────────────────────────────

    public void GoAway()
    {
        if (_isLeaving) return;
        _isLeaving = true;

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

    // ─── Fila do caixa ────────────────────────────────────────────────────────

    public void SetQueueTarget(Transform target, int index)
    {
        _queueIndex = index;
        HasArrivedAtQueueTarget = false;

        if (_queueWaiter != null)
            StopCoroutine(_queueWaiter);

        _agent.isStopped = false;
        _agent.SetDestination(target.position);
        _queueWaiter = StartCoroutine(WaitUntilAtQueuePosition(target));
    }

    private IEnumerator WaitUntilAtQueuePosition(Transform target)
    {
        yield return new WaitUntil(() => !_agent.pathPending);

        float threshold = Mathf.Max(_agent.stoppingDistance, 0.05f);
        while (Vector3.Distance(transform.position, target.position) > threshold)
            yield return null;

        _agent.isStopped = true;
        HasArrivedAtQueueTarget = true;
        _queueWaiter = null;
    }

    public void SetTarget(Transform target, int index) => SetQueueTarget(target, index);

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

    /// <summary>
    /// Pega de 1 a N itens da prateleira (quantidade sorteada por chance),
    /// respeitando o limite total de itens que o NPC carrega na sacola.
    /// Para cedo se a prateleira ficar vazia no meio da coleta.
    /// </summary>
    private void CollectItemsFromShelf(Shelf shelf)
    {
        var globalPrices = ServiceLocator.Get<GlobalPrices>();
        int quantity = RollItemQuantity();

        for (int i = 0; i < quantity && _inventory.Count < _maxInventorySize; i++)
        {
            Items item = shelf.TakeRandomItem();
            if (item == Items.None) break;

            float price = globalPrices.GetItemCurrentPrice(item);
            _inventory.Add(new ShoppingItem(item, price));
            Debug.Log($"[NPC] Pegou: {item}");
        }
    }

    /// <summary>
    /// Sorteia quantos itens o NPC pega de uma furniture, com base nos pesos
    /// configurados em <see cref="_itemsPerFurnitureWeights"/>.
    /// </summary>
    private int RollItemQuantity()
    {
        if (_itemsPerFurnitureWeights == null || _itemsPerFurnitureWeights.Length == 0)
            return 1;

        return _itemsPerFurnitureWeights[Random.Range(0, _itemsPerFurnitureWeights.Length)];
    }

    /// <summary>Item comprado pelo NPC, com o preço travado no momento da compra.</summary>
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