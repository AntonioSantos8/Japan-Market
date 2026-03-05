using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrashSystem : MonoBehaviour
{
    [SerializeField] private Transform[] spawns;
    [SerializeField] private MarketManager marketManager;
    [SerializeField] private TrashData[] trashDatas;
    [SerializeField] private int maxTrashCount = 10;

    private List<GameObject> activeTrashes = new List<GameObject>();

    private void Start() => StartSpawningTrash();

    public void StartSpawningTrash() => StartCoroutine(SpawnTrashRoutine());

    private IEnumerator SpawnTrashRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(20, 30));

            if (marketManager.open && activeTrashes.Count < maxTrashCount)
            {
                Spawn();
            }
        }
    }

    private void Spawn()
    {
        int spawnIndex = Random.Range(0, spawns.Length);
        int dataIndex = Random.Range(0, trashDatas.Length);

        GameObject go = Instantiate(trashDatas[dataIndex].prefab, spawns[spawnIndex].position, Quaternion.identity);

        if (go.TryGetComponent(out TrashInstance instance))
        {
            instance.TrashData = trashDatas[dataIndex];
        }

        activeTrashes.Add(go);
    }

    public void UnregisterTrash(GameObject trash)
    {
        if (activeTrashes.Contains(trash))
            activeTrashes.Remove(trash);
    }
}