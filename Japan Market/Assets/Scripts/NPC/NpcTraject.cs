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
    private void SortTraject()
    {
        if (ServiceLocator.Get<FurnituresManager>().furnitures == null) return;

        GameObject furniture = ServiceLocator.Get<FurnituresManager>().
            furnitures[Random.Range(0, ServiceLocator.Get<FurnituresManager>().furnitures.Count)];
        Transform frontPoint = furniture.transform.Find("Front");
        npc_agent.SetDestination(frontPoint.transform.position);
    }
}
