using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;
using UnityEngine;
using UnityEngine.AI;

namespace JapanMarket.Gameplay
{

    [DisallowMultipleComponent]
    public sealed class CustomerSpawner : MonoBehaviour
    {
        [Header("Prefabs and profiles")]
        [SerializeField] private CustomerAgent[] _customerPrefabs;

        [Tooltip("Randomly chosen per customer. Empty = uses the prefab's own profile.")]
        [SerializeField] private CustomerProfileData[] _profiles;

        [Header("Points")]
        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private Transform _entryPoint;
        [SerializeField] private Transform _exitPoint;

        [Tooltip("Dispersion radius on spawn, so they don't spawn overlapping.")]
        [SerializeField] private float _spawnRadius = 0.6f;

        [Header("Rhythm")]
        [SerializeField] private Vector2 _intervalSeconds = new(8f, 25f);

        [Tooltip("Maximum live customers at the same time. 0 = unlimited.")]
        [Min(0)] [SerializeField] private int _maxAlive = 6;

        [Header("Limpeza")]
        [Min(1f)] [SerializeField] private float _dirtyIntervalMultiplier = 3f;
        [Range(0.1f, 1f)] [SerializeField] private float _dirtyCapacityFraction = 0.5f;

        [Header("Start")]
        [Tooltip("Starts spawning without waiting for the store to open. Use in Sandbox.")]
        [SerializeField] private bool _autoStart;

        private readonly List<CustomerAgent> _alive = new();
        private System.IDisposable _openSubscription;
        private float _nextSpawnAt;
        private bool _running;

        private void OnEnable()
        {
            TrySubscribe();
            if (_autoStart) StartSpawning();
        }

        private void TrySubscribe()
        {
            if (_openSubscription != null) return;

            GameContext game = GameContext.Current;
            if (game == null) return;

            _openSubscription = game.Events.Subscribe<StoreOpenStateChanged>(OnStoreOpenChanged);
        }

        private void OnDisable()
        {
            _openSubscription?.Dispose();
            _openSubscription = null;
            StopSpawning();
        }

        private void OnStoreOpenChanged(StoreOpenStateChanged evt)
        {
            if (evt.IsOpen) StartSpawning();
            else
            {
                StopSpawning();
                NotifyAll(Domain.CustomerSignal.StoreClosed);
            }
        }

        public void StartSpawning()
        {
            _running = true;
            ScheduleNext();
        }

        public void StopSpawning() => _running = false;

        private void Update()
        {
            TrySubscribe();

            if (!_running) return;

            PruneDead();

            if (Time.time < _nextSpawnAt) return;
            int capacity = _maxAlive > 0
                ? Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(_maxAlive,
                    _maxAlive * _dirtyCapacityFraction, DirtLevel)))
                : 0;
            if (capacity > 0 && _alive.Count >= capacity) { ScheduleNext(); return; }

            Spawn();
            ScheduleNext();
        }

        private float DirtLevel => GameContext.Current?.Cleanliness?.Normalized ?? 0f;

        private void ScheduleNext() =>
            _nextSpawnAt = Time.time + Random.Range(_intervalSeconds.x, _intervalSeconds.y)
                * Mathf.Lerp(1f, _dirtyIntervalMultiplier, DirtLevel);

        private void Spawn()
        {
            if (!ValidateSetup()) { StopSpawning(); return; }

            CustomerAgent prefab = _customerPrefabs[Random.Range(0, _customerPrefabs.Length)];
            if (prefab == null) return;   

            if (!TryFindSpawnPosition(out Vector3 position)) return;

            CustomerAgent customer = Instantiate(prefab, position, _spawnPoint.rotation);

            CustomerProfileData profile = _profiles != null && _profiles.Length > 0
                ? _profiles[Random.Range(0, _profiles.Length)]
                : null;

            customer.Initialize(_entryPoint.position, _exitPoint.position, profile);
            _alive.Add(customer);
        }

        private bool TryFindSpawnPosition(out Vector3 position)
        {
            Vector2 offset = Random.insideUnitCircle * _spawnRadius;
            Vector3 candidate = _spawnPoint.position + new Vector3(offset.x, 0f, offset.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                position = hit.position;
                return true;
            }

            Debug.LogWarning("[CustomerSpawner] Ponto de spawn fora da NavMesh. " +
                             "Verifique se a malha foi assada.", this);
            position = candidate;
            return false;
        }

        private bool ValidateSetup()
        {
            if (_customerPrefabs == null || _customerPrefabs.Length == 0)
            {
                Debug.LogError("[CustomerSpawner] No customer prefab assigned.", this);
                return false;
            }

            if (_spawnPoint == null || _entryPoint == null || _exitPoint == null)
            {
                Debug.LogError("[CustomerSpawner] Missing spawn, entry or exit points.", this);
                return false;
            }

            return true;
        }

        private void PruneDead() => _alive.RemoveAll(c => c == null);

        private void NotifyAll(Domain.CustomerSignal signal)
        {
            PruneDead();
            for (int i = 0; i < _alive.Count; i++) _alive[i].Notify(signal);
        }

        [ContextMenu("Spawn a customer now")]
        public void SpawnOneNow()
        {
            if (!Application.isPlaying) return;
            PruneDead();
            Spawn();
        }
    }
}
