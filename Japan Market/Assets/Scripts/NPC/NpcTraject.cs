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

    [Header("Exit Config")]
    [Tooltip("Distância do Exit em que o NPC é destruído (resolve bug de aglomeração na saída).")]
    [SerializeField] private float _exitDestroyDistance = 1.5f;

    // Referência ao exit point
    private Transform _exitPoint;

    // Inventário: item → preço no momento da compra
    private readonly Dictionary<Items, float> _inventory = new Dictionary<Items, float>();

    // Slot reservado na furniture atual
    private FurnitureOccupancy _currentOccupancy;
    private Vector3 _reservedSlotPosition;

    // Controle de caixa
    private bool _itemsPlaced = false;
    private int _queueIndex  = -1;

    private bool _isLeaving = false;
    private Coroutine _queueWaiter;

    // ─── Unity ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _agent        = GetComponent<NavMeshAgent>();
        _npcInstance  = GetComponent<NpcInstance>();
    }

    private void Start()
    {
        _exitPoint      = GameObject.FindGameObjectWithTag("Exit").transform;
        _cashRegister   = ServiceLocator.Get<CashRegister>();
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
                && _cashRegister.hasClient)
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

        foreach (var pair in _inventory)
        {
            _cashRegister.SpawnItemWithAnimation(pair.Key, pair.Value);
            yield return new WaitForSeconds(0.2f);
        }

        _inventory.Clear();
        Debug.Log("[NPC] Itens colocados no balcão.");
    }

    // ─── Trigger (área do caixa) ──────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("CashRegister"))
            _cashRegister.hasClient = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("CashRegister"))
            _cashRegister.hasClient = false;
    }

    // ─── Rotina principal de compras ──────────────────────────────────────────

    private IEnumerator ShoppingRoutine()
    {
        yield return new WaitForSeconds(Random.Range(2f, 5f)); // leve variação no start

        var allFurnitures = _furnitureManager.GetPlacedFurnitures();

        if (allFurnitures != null && allFurnitures.Count > 0)
        {
            int quantToVisit = Random.Range(1, Mathf.Min(allFurnitures.Count + 1, 6));
            var selected     = PickFurnitures(allFurnitures, quantToVisit);

            foreach (var furniture in selected)
            {
                // Tenta reservar um slot nesta furniture
                var occupancy = furniture.GetComponent<FurnitureOccupancy>();

                if (occupancy != null)
                {
                    if (!occupancy.TryReserve(out _reservedSlotPosition))
                    {
                        // Furniture lotada — pula para a próxima
                        Debug.Log("[NPC] Furniture lotada, pulando.");
                        continue;
                    }
                    _currentOccupancy = occupancy;
                }
                else
                {
                    // Furniture sem FurnitureOccupancy: usa InteractionPosition diretamente
                    _reservedSlotPosition = furniture.InteractionPosition;
                    _currentOccupancy = null;
                }

                yield return StartCoroutine(GoToDest(_reservedSlotPosition));

                // Pega item se possível
                if (furniture.shelf != null && _inventory.Count < 5)
                {
                    Items item = furniture.shelf.TakeRandomItem();
                    if (item != Items.None)
                    {
                        float price = ServiceLocator.Get<GlobalPrices>().GetItemCurrentPrice(item);
                        _inventory[item] = price;
                        Debug.Log($"[NPC] Pegou: {item}");
                    }
                }

                yield return new WaitForSeconds(_waitTimeAtShelf);

                // Libera slot
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

        // Aguarda o agente começar a se mover
        yield return new WaitForSeconds(0.3f);
        yield return new WaitUntil(() => !_agent.pathPending);

        // Destroi por distância em vez de esperar chegar no ponto exato.
        // Isso evita que vários NPCs fiquem travados esperando a vez no exit.
        while (true)
        {
            float dist = Vector3.Distance(transform.position, _exitPoint.position);
            if (dist <= _exitDestroyDistance) break;

            // Segurança: se o agente ficou parado por muito tempo longe do exit,
            // força destruição para não vazar NPC na cena.
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
        _queueWaiter = null;
    }

    // Mantém compatibilidade com o nome anterior usado pelo CashRegister
    public void SetTarget(Transform target, int index) => SetQueueTarget(target, index);

    // ─── Movimento genérico ───────────────────────────────────────────────────

    private IEnumerator GoToDest(Vector3 dest)
    {
        _agent.SetDestination(dest);

        yield return new WaitUntil(() => !_agent.pathPending);

        // Aguarda chegar: usa margem levemente maior que stoppingDistance para não travar
        float arrivalThreshold = _agent.stoppingDistance + 0.05f;

        while (_agent.remainingDistance > arrivalThreshold)
            yield return null;
    }

    // ─── Seleção de furnitures ────────────────────────────────────────────────

    private List<FurnitureInstance> PickFurnitures(List<FurnitureInstance> source, int count)
    {
        var copy   = new List<FurnitureInstance>(source);
        var result = new List<FurnitureInstance>();

        for (int i = 0; i < count && copy.Count > 0; i++)
        {
            int idx = Random.Range(0, copy.Count);
            result.Add(copy[idx]);
            copy.RemoveAt(idx);
        }

        return result;
    }
}