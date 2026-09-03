using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrashSystem : MonoBehaviour
{
    [SerializeField] private TrashData[] trashDatas;
    [SerializeField] private int maxTrashCount = 8;

    [Header("Spawn")]
    [Tooltip("Intervalo (em segundos) entre cada tentativa de spawnar lixo.")]
    [SerializeField] private float _spawnInterval = 20f;
    [Tooltip("Chance (0-1) de spawnar lixo a cada tentativa, aplicada uma única vez por tentativa — não por cliente.")]
    [SerializeField] private float _spawnChance = 1f / 8f;
    [Tooltip("Raio ao redor do cliente onde o lixo pode aparecer.")]
    [SerializeField] private float _spawnRadius = 1.5f;
    [Tooltip("Layers que representam o chão para o raycast de spawn.")]
    [SerializeField] private LayerMask _floorMask = ~0;
    [Tooltip("Layers de obstáculos que impedem o spawn (paredes, prateleiras, etc).")]
    [SerializeField] private LayerMask _obstacleMask;

    private readonly List<GameObject> activeTrashes = new List<GameObject>();
    private readonly HashSet<Transform> _trashSpawners = new HashSet<Transform>();

    private void Awake() => ServiceLocator.Register(this);
    private void Start() => StartSpawningTrash();

    public void StartSpawningTrash() => StartCoroutine(SpawnTrashRoutine());

    private IEnumerator SpawnTrashRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(_spawnInterval);

            MarketManager market = ServiceLocator.Get<MarketManager>();
            if (!market.Open || market.Clients == 0) continue;
            if (activeTrashes.Count >= maxTrashCount) continue;

            // Remove referências de clientes já destruídos
            _trashSpawners.RemoveWhere(t => t == null);

            // Escolhe no máximo um cliente elegível e rola a chance uma única vez —
            // antes a chance era rolada por cliente, então mais clientes na loja
            // significava muito mais lixo (efetivamente bem acima do valor configurado).
            var eligible = new List<Transform>();
            foreach (var client in market.ClientTransforms)
            {
                if (client == null) continue;
                if (_trashSpawners.Contains(client)) continue;
                eligible.Add(client);
            }

            if (eligible.Count == 0) continue;
            if (Random.value > _spawnChance) continue;

            var chosen = eligible[Random.Range(0, eligible.Count)];
            _trashSpawners.Add(chosen);
            if (TryFindSpawnPosition(chosen.position, out Vector3 spawnPos))
                SpawnAt(spawnPos);
        }
    }

    // Tenta encontrar uma posição livre no chão ao redor do cliente.
    private bool TryFindSpawnPosition(Vector3 clientPos, out Vector3 result)
    {
        for (int i = 0; i < 12; i++)
        {
            Vector2 circle = Random.insideUnitCircle.normalized * Random.Range(0.4f, _spawnRadius);
            Vector3 origin = clientPos + new Vector3(circle.x, 1.5f, circle.y);

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 3f, _floorMask))
            {
                // Sobe ligeiramente do ponto de impacto para não cravar no chão
                Vector3 pos = hit.point + hit.normal * 0.03f;

                // Garante que não há obstáculo no local
                if (_obstacleMask == 0 || !Physics.CheckSphere(pos, 0.2f, _obstacleMask))
                {
                    result = pos;
                    return true;
                }
            }
        }

        // Fallback: posição do cliente (comportamento original)
        result = clientPos;
        return true;
    }

    private void SpawnAt(Vector3 position)
    {
        if (trashDatas.Length == 0) return;

        int dataIndex = Random.Range(0, trashDatas.Length);
        GameObject go = Instantiate(trashDatas[dataIndex].prefab, position, Quaternion.identity, transform);

        if (go.TryGetComponent(out TrashInstance instance))
            instance.TrashData = trashDatas[dataIndex];

        activeTrashes.Add(go);
    }

    public void UnregisterTrash(GameObject trash) => activeTrashes.Remove(trash);
}
