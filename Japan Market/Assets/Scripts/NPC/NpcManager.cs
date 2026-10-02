using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Gerencia o ciclo de spawn de NPCs.
/// Adições:
/// - Limite máximo de NPCs simultâneos na cena (_maxNpcsInScene).
/// - Rastreia NPCs ativos para não spawnar infinitamente.
/// - Leve offset aleatório no ponto de spawn para evitar que NPCs nasçam sobrepostos.
/// </summary>
public class NpcManager : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject[] _npcPrefabs;

    [Header("Spawn Point")]
    [SerializeField] private Transform _spawnPoint;
    [Tooltip("Raio de variação aleatória ao redor do spawnPoint.")]
    [SerializeField] private float _spawnRadius = 0.5f;

    [Header("Spawn Timing")]
    [SerializeField] private float _minSpawnInterval = 10f;
    [SerializeField] private float _maxSpawnInterval = 60f;

    [Header("Crowd Control")]
    [Tooltip("Número máximo de NPCs vivos ao mesmo tempo. 0 = sem limite.")]
    [SerializeField] private int _maxNpcsInScene = 5;

    [Header("Limpeza")]
    [Min(1f)] [SerializeField] private float _dirtyIntervalMultiplier = 3f;
    [Range(0.1f, 1f)] [SerializeField] private float _dirtyCapacityFraction = 0.5f;

    private readonly List<GameObject> _activeNpcs = new List<GameObject>();

    private void Start()
    {
        ServiceLocator.Register(this);
    }
    private Coroutine _spawnCoroutine;

    public void StartSpawning()
    {
        if (_spawnCoroutine != null)
            StopCoroutine(_spawnCoroutine);

        _spawnCoroutine = StartCoroutine(SpawnRoutine());
    }

    public void StopSpawning()
    {
        if (_spawnCoroutine != null)
        {
            StopCoroutine(_spawnCoroutine);
            _spawnCoroutine = null;
        }
    }
    private IEnumerator SpawnRoutine()
    {
        while (true)
        {
            if (!ServiceLocator.Get<MarketManager>().Open)
                yield break;

            float interval = Random.Range(_minSpawnInterval, _maxSpawnInterval)
                * Mathf.Lerp(1f, _dirtyIntervalMultiplier, DirtLevel);
            yield return new WaitForSeconds(interval);

            CleanupDestroyedNpcs();

            int capacity = _maxNpcsInScene > 0
                ? Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(_maxNpcsInScene,
                    _maxNpcsInScene * _dirtyCapacityFraction, DirtLevel)))
                : 0;
            if (capacity > 0 && _activeNpcs.Count >= capacity)
            {
                Debug.Log("[NpcManager] Cap de NPCs atingido, aguardando...");
                yield return null;
                continue;
            }

            SpawnNpc();
        }
    }
    private float DirtLevel => JapanMarket.Gameplay.GameContext.Current?.Cleanliness?.Normalized ?? 0f;

    private void SpawnNpc()
    {
        if (_npcPrefabs == null || _npcPrefabs.Length == 0 || _spawnPoint == null)
            return;

        int idx = Random.Range(0, _npcPrefabs.Length);
        GameObject prefab = _npcPrefabs[idx];
        if (prefab == null) return;
        NavMeshAgent prefabAgent = prefab.GetComponent<NavMeshAgent>();
        if (prefabAgent == null) return;

        Vector2 circle = Random.insideUnitCircle * _spawnRadius;
        Vector3 spawnPos = _spawnPoint.position + new Vector3(circle.x, 0f, circle.y);

        var filter = new NavMeshQueryFilter { agentTypeID = prefabAgent.agentTypeID, areaMask = prefabAgent.areaMask };
        float sampleRadius = Mathf.Max(1.25f, _spawnRadius + 0.5f);
        if (!NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, sampleRadius, filter)
            && !NavMesh.SamplePosition(_spawnPoint.position, out hit, sampleRadius, filter))
        {
            Debug.LogWarning("[NpcManager] Spawn sem NavMesh próxima; NPC não criado.", this);
            return;
        }

        // SamplePosition returns the mesh surface, while the agent's transform
        // sits above it by baseOffset (1m in the NPC prefabs).
        Vector3 agentPosition = hit.position + Vector3.up * prefabAgent.baseOffset;
        GameObject npc = Instantiate(prefab, agentPosition, _spawnPoint.rotation);
        NavMeshAgent agent = npc.GetComponent<NavMeshAgent>();
        if (!agent.Warp(agentPosition))
        {
            Destroy(npc);
            return;
        }
        _activeNpcs.Add(npc);

        TutorialManager tutorialManager = ServiceLocator.Get<TutorialManager>();
        if (tutorialManager != null)
            tutorialManager.NotifyGameEvent("ClientCame");
    }

    /// <summary>Remove entradas nulas (NPCs já destruídos) da lista.</summary>
    private void CleanupDestroyedNpcs()
    {
        _activeNpcs.RemoveAll(n => n == null);
    }
}
