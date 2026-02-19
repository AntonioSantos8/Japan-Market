using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class NpcTraject : MonoBehaviour
{
    [SerializeField] private FurnituresManager furnituresManager;
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
        Transform furniture = furnituresManager.furnitures[Random.Range(0, furnituresManager.furnitures.Count)];
        npc_agent.SetDestination(furniture.position);
    }
}
