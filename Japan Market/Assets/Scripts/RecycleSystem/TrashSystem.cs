using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrashSystem : MonoBehaviour
{
    [SerializeField] private TrashData[] trashDatas;
    [SerializeField] private int maxTrashCount = 10;

    private List<GameObject> activeTrashes = new List<GameObject>();
    private HashSet<Transform> _trashSpawners = new HashSet<Transform>();
    private void Start() => StartSpawningTrash();

    public void StartSpawningTrash() => StartCoroutine(SpawnTrashRoutine());

    private IEnumerator SpawnTrashRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(10f);
            print("Tentando spawnar lixo...");
            MarketManager market = ServiceLocator.Get<MarketManager>();

            if (!market.Open || market.Clients == 0)
                continue;

            if (activeTrashes.Count >= maxTrashCount)
                continue;

            foreach (var client in market.ClientTransforms)
            {
                if (client == null) continue;
                if (_trashSpawners.Contains(client)) continue;

                if (Random.value <= 1f / 6f)
                {
                    _trashSpawners.Add(client);
                    SpawnAt(client.position);
                    break;
                }
            }
        }
    }

    private void SpawnAt(Vector3 position)
    {
        if (trashDatas.Length == 0) return;

        int dataIndex = Random.Range(0, trashDatas.Length);

        GameObject go = Instantiate(
            trashDatas[dataIndex].prefab,
            position,
            Quaternion.identity,
            this.transform
        );

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