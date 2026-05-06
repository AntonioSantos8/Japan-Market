using System.Collections;
using UnityEngine;

public class NpcManager : MonoBehaviour
{
    [SerializeField] private GameObject[] npcCommonPrefab;
    [SerializeField] private Transform spawnPoint;

    [SerializeField] private float minSpawnInterval = 10f;
    [SerializeField] private float maxSpawnInterval = 60f;
    private void Start()
    {
        
        StartCoroutine(SpawnRoutine());
    }

    private void SpawnNpc()
    {
        int randomIndex = Random.Range(0, npcCommonPrefab.Length);
        if (npcCommonPrefab != null && spawnPoint != null)
        {
            Instantiate(npcCommonPrefab[randomIndex], spawnPoint.position, spawnPoint.rotation);
        }
    }
    private IEnumerator SpawnRoutine()
    {
        while (true)
        {
            float randomInterval = Random.Range(minSpawnInterval, maxSpawnInterval);
            yield return new WaitForSeconds(randomInterval);
            SpawnNpc();
        }
    }
}
