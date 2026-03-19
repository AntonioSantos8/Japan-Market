using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NpcTraject : MonoBehaviour
{
    private NavMeshAgent _agent;
    private FurnitureManager _furnitureManager;

    [Header("Dest config")]
    private Transform _finalPoint;
    [SerializeField] private Transform someExitPoint;
    [SerializeField] private float _waitTime = 5;
    [Header("Queue config")]
    private int _queueIndex = -1;

    [Header("Inventory")]
    private List<Items> _inventory = new List<Items>();
    private CashRegister _cashRegister;
    private bool _itemsPlaced = false;
    private NpcInstance _npcInstance;
    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _npcInstance = GetComponent<NpcInstance>();
    }
    private void Start()
    {
        _cashRegister = ServiceLocator.Get<CashRegister>();
        _furnitureManager = ServiceLocator.Get<FurnitureManager>();
        _finalPoint = _cashRegister.queuePoints[_cashRegister.queuePoints.Length - 1];
        if (_furnitureManager == null)
        {
            _furnitureManager = FindAnyObjectByType<FurnitureManager>();
            if (_furnitureManager == null)
            {
                Debug.LogError("FurnitureManager not found in the scene.");
                return;
            }
        }
        StartCoroutine(WaitInFrontOfCashRegister());
        StartCoroutine(MoveRoutine());
    }
    private IEnumerator WaitInFrontOfCashRegister()
    {
        while (true)
        {
            VerifyIsInCashRegister();
            yield return new WaitForSeconds(1f);
        }
    }
    private void VerifyIsInCashRegister()
    {
        if (!_itemsPlaced && _cashRegister.GetCurrentCustomer() == this && _cashRegister.hasClient)
        {
            PlaceItemsOnCounter();
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

        for (int i = 0; i < _inventory.Count; i++)
        {
            Items itemType = _inventory[i];

            _cashRegister.SpawnItemWithAnimation(itemType);

            yield return new WaitForSeconds(0.2f);
        }

        _inventory.Clear();
        Debug.Log("NPC terminou de colocar os itens no balcão.");
    }
    #region Trigger
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("CashRegister"))
        {
            print("Entrou no caixa");
            _cashRegister.hasClient = true;
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("CashRegister"))
        {
            _cashRegister.hasClient = false;
        }
    }
    #endregion
    private IEnumerator MoveRoutine()
    {
        if (_furnitureManager == null)
        {
            yield break;
        }

        yield return new WaitForSeconds(4f);

        var allFurnitures = _furnitureManager.GetPlacedFurnitures();

        if (allFurnitures != null && allFurnitures.Count > 0)
        {
            int quantToVisit = Random.Range(1, allFurnitures.Count + 1);
            List<FurnitureInstance> sortList = SortFurniture(allFurnitures, quantToVisit);

            foreach (var furniture in sortList)
            {
                yield return StartCoroutine(GoToDest(furniture.InteractionPosition));

                if (furniture.shelf != null)
                {
                    if (_inventory.Count < 5)
                    {
                        Items item = furniture.shelf.TakeRandomItem();
                        if (item != Items.None)
                        {
                            _inventory.Add(item);
                            Debug.Log("NPC pegou: " + item);
                        }
                    }
                }

                yield return new WaitForSeconds(_waitTime);
            }
        }

        if (_inventory.Count == 0)
        {
            Debug.Log("NPC não encontrou itens e está indo embora direto.");
            GoAway();
            yield break;
        }

        if (_finalPoint != null)
        {
            yield return StartCoroutine(GoToDest(_finalPoint.position));
            _cashRegister.EnterQueue(this);
            Debug.Log("NPC entrou na fila");
        }
    }
    public void GoAway()
    {
        _agent.SetDestination(someExitPoint.position);
    }
    public void SetTarget(Transform target, int index)
    {
        _queueIndex = index;
        _agent.SetDestination(target.position);
    }
    private IEnumerator GoToDest(Vector3 dest)
    {
        _agent.SetDestination(dest);

        yield return new WaitUntil(() => !_agent.pathPending);

        while (_agent.remainingDistance > _agent.stoppingDistance)
        {
            yield return null;
        }
    }

    private List<FurnitureInstance> SortFurniture(List<FurnitureInstance> originalList, int quant)
    {
        List<FurnitureInstance> copy = new List<FurnitureInstance>(originalList);
        List<FurnitureInstance> result = new List<FurnitureInstance>();

        for (int i = 0; i < quant; i++)
        {
            if (copy.Count == 0) break;

            int index = Random.Range(0, copy.Count);
            result.Add(copy[index]);
            copy.RemoveAt(index);
        }

        return result;
    }
}