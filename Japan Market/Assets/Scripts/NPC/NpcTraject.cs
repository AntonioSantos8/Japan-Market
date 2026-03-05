using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NpcTraject : MonoBehaviour
{
    private NavMeshAgent _agent;
    private FurnitureManager _furnitureManager;

    [Header("Dest config")]
    [SerializeField] private Transform _finalPoint;
    [SerializeField] private float _waitTime = 2f;

    private void Awake() => _agent = GetComponent<NavMeshAgent>();

    private void Start()
    {
        _furnitureManager = ServiceLocator.Get<FurnitureManager>();
        StartCoroutine(MoveRotine());
    }

    private IEnumerator MoveRotine()
    {
        yield return new WaitForSeconds(4f);

        var allFurnitures = _furnitureManager.GetPlacedFurnitures();

        if (allFurnitures != null && allFurnitures.Count > 0)
        {
            int quantToVisit = Random.Range(1, allFurnitures.Count + 1);

            List<FurnitureInstance> sortList = SortFurniture(allFurnitures, quantToVisit);
            foreach (var furniture in sortList)
            {
                yield return StartCoroutine(GoToDest(furniture.InteractionPosition));

                yield return new WaitForSeconds(_waitTime);
            }
        }

        if (_finalPoint != null)
        {
            yield return StartCoroutine(GoToDest(_finalPoint.position));
        }
    }

    private IEnumerator GoToDest(Vector3 dest)
    {
        _agent.SetDestination(dest);

        yield return new WaitUntil(() => !_agent.pathPending);
            
        yield return new WaitUntil(() => _agent.remainingDistance <= _agent.stoppingDistance);
        print("Chegoy");
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