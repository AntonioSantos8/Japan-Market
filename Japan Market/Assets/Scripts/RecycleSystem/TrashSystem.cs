using System.Collections;
using UnityEngine;

public class TrashSystem : MonoBehaviour
{
    [SerializeField] private Transform[] spawns;

    [SerializeField] private MarketManager marketManager;
    [SerializeField] private TrashData[] trashDatas;
    private void Start()
    {
        StartSpawningTrash();
    }
    public void StartSpawningTrash()
    {
        StartCoroutine(SpawnTrash());
    }
    private IEnumerator SpawnTrash()
    {
        if(marketManager.open == false) yield break;
        while(marketManager.open)
        {
            int timeToSpawn = Random.Range(30, 40);
            int spawnPlaceIndex = Random.Range(spawns.Length, 0);
            int trashPrefab = Random.Range(trashDatas.Length, 0);
            yield return new WaitForSeconds(timeToSpawn);
            Instantiate(trashDatas[trashPrefab].prefab, spawns[spawnPlaceIndex]);
        }
    }
}
