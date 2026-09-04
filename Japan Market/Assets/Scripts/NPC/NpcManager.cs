using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    private readonly List<GameObject> _activeNpcs = new List<GameObject>();

    private void Awake()
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
        MarketManager market = ServiceLocator.Get<MarketManager>();
        if (market == null)
            Debug.LogError("[NpcManager] MarketManager nao registrado; a rotina vai spawnar sem checar Open.");

        while (true)
        {
            // Se o MarketManager ainda nao existia quando a rotina arrancou, tenta de novo.
            if (market == null) market = ServiceLocator.Get<MarketManager>();

            // So aborta quando temos a certeza de que a loja esta fechada. Um null aqui
            // costumava lancar NullReferenceException e matar a corrotina em silencio.
            if (market != null && !market.Open)
                yield break;

            float interval = Random.Range(_minSpawnInterval, _maxSpawnInterval);
            yield return new WaitForSeconds(interval);

            CleanupDestroyedNpcs();

            if (_maxNpcsInScene > 0 && _activeNpcs.Count >= _maxNpcsInScene)
            {
                Debug.Log("[NpcManager] Cap de NPCs atingido, aguardando...");
                yield return null;
                continue;
            }

            SpawnNpc();
        }
    }

    private void SpawnNpc()
    {
        if (_npcPrefabs == null || _npcPrefabs.Length == 0 || _spawnPoint == null)
            return;

        int idx = Random.Range(0, _npcPrefabs.Length);

        Vector2 circle = Random.insideUnitCircle * _spawnRadius;
        Vector3 spawnPos = _spawnPoint.position + new Vector3(circle.x, 0f, circle.y);

        GameObject npc = Instantiate(_npcPrefabs[idx], spawnPos, _spawnPoint.rotation);
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