using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NpcTraject : MonoBehaviour
{
    private NavMeshAgent _agent;
    private FurnitureManager _furnitureManager;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        _furnitureManager = ServiceLocator.Get<FurnitureManager>();
        Invoke(nameof(SortTraject), 2f);
    }

    public void SortTraject()
    {
        var allFurniture = _furnitureManager.GetPlacedFurnitures();

        if (allFurniture == null || allFurniture.Count == 0)
        {
            Debug.LogWarning("Npc nao achou nd");
            return;
        }

        int randomIndex = Random.Range(0, allFurniture.Count);
        FurnitureInstance targetFurniture = allFurniture[randomIndex];

        _agent.SetDestination(targetFurniture.InteractionPosition);
    }
}