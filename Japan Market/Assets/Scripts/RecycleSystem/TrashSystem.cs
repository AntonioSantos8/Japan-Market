using System.Collections;
using UnityEngine;

public class TrashSystem : MonoBehaviour
{
    [SerializeField] private GameObject trashPrefab;
    [SerializeField] private Transform[] spawns;

    private MarketManager marketStatus;
    private void Start()
    {
        StartCoroutine(SpawnTrash());
    }
    private IEnumerator SpawnTrash()
    {
        if(marketStatus.open == false) yield break;
        while(marketStatus.open)
        {
            int timeToSpawn = Random.Range(30, 40);
            int spawnPlaceIndex = Random.Range(spawns.Length, 0);
            yield return new WaitForSeconds(timeToSpawn);
            print("oi");
            if(trashPrefab  != null) 
            Instantiate(trashPrefab, spawns[spawnPlaceIndex]);
        }
    }
}
