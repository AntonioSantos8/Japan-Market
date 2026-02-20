using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class NpcTraject : MonoBehaviour
{
    private NavMeshAgent npc_agent;

    private void Awake()
    {
        npc_agent = GetComponent<NavMeshAgent>();
    }
    private void Start()
    {
        SortTraject();
    }
    private void SortTraject()
    {
        GameObject furniture = ServiceLocator.Get<FurnituresManager>().
            furnitures[Random.Range(0, ServiceLocator.Get<FurnituresManager>().furnitures.Count)];
        npc_agent.SetDestination(furniture.transform.position);
    }
}
